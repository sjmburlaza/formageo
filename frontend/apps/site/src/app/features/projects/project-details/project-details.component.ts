import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  HostListener,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ComparisonsApiService,
  getApiErrorMessage,
  LayersApiService,
  ProjectsApiService,
  ReportsApiService,
  ScoringApiService,
  SitesApiService,
} from '@frontend/api-client';
import {
  MapComponent,
  MapFeature,
  MapFeatureSelectionEvent,
  MapGeometryEvent,
  MapInteractionMode,
  MapOverlay,
  MapOverlayStateChange,
} from '@frontend/map';
import {
  AnalysisEvidenceResult,
  ComparisonSummary,
  GeoJsonPolygon,
  LayerDefinition,
  Project,
  ProjectLayerPreference,
  ScoringScenario,
  Site,
  SiteSummary,
} from '@frontend/models';
import {
  AlertComponent,
  EmptyStateComponent,
  PageStateComponent,
  StatusBadgeComponent,
} from '@frontend/ui';
import {
  LucideArrowLeft,
  LucideArrowRight,
  LucideCircleCheck,
  LucideColumns3,
  LucideDownload,
  LucidePenTool,
  LucideUpload,
  LucideX,
} from '@lucide/angular';
import { finalize, forkJoin } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';
import { SiteImportWizardComponent } from '../../sites/site-import/site-import-wizard.component';
import { SiteImportCompletedEvent } from '../../sites/site-import/site-import.models';
import { SiteAnalysisComponent } from '../../analyses/site-analysis.component';
import { ProjectSiteListComponent } from './project-site-list/project-site-list.component';
import { ProjectSiteSummaryComponent } from './project-site-summary/project-site-summary.component';

type InspectorTab = 'overview' | 'boundary' | 'analysis' | 'history';
type SavingAction = 'rename' | 'boundary' | 'archive' | 'restore' | 'delete';

@Component({
  selector: 'fg-project-details',
  standalone: true,
  imports: [
    AlertComponent,
    CommonModule,
    EmptyStateComponent,
    LucideArrowLeft,
    LucideArrowRight,
    LucideCircleCheck,
    LucideColumns3,
    LucideDownload,
    LucidePenTool,
    LucideUpload,
    LucideX,
    MapComponent,
    PageStateComponent,
    ProjectSiteListComponent,
    ProjectSiteSummaryComponent,
    ReactiveFormsModule,
    RouterLink,
    SiteAnalysisComponent,
    SiteImportWizardComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './project-details.component.html',
  styleUrl: './project-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailsComponent implements OnInit {
  @ViewChild(MapComponent)
  private mapComponent?: MapComponent;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly projectsApi = inject(ProjectsApiService);
  private readonly sitesApi = inject(SitesApiService);
  private readonly layersApi = inject(LayersApiService);
  private readonly scoringApi = inject(ScoringApiService);
  private readonly comparisonsApi = inject(ComparisonsApiService);
  private readonly reportsApi = inject(ReportsApiService);
  private summaryRequestSequence = 0;
  private layerPreferencesSaveTimer?: ReturnType<typeof setTimeout>;

  protected readonly project = signal<Project | null>(null);
  protected readonly sites = signal<Site[]>([]);
  protected readonly layerOverlays = signal<MapOverlay[]>([]);
  protected readonly analysisOverlays = signal<MapOverlay[]>([]);
  protected readonly mapOverlays = computed(() => [
    ...this.layerOverlays(),
    ...this.analysisOverlays(),
  ]);
  protected readonly selectedSiteId = signal<string | null>(null);
  protected readonly selectedSite = computed(
    () =>
      this.sites().find((site) => site.id === this.selectedSiteId()) ?? null,
  );
  protected readonly editingSiteId = signal<string | null>(null);
  protected readonly editingSite = computed(
    () => this.sites().find((site) => site.id === this.editingSiteId()) ?? null,
  );
  protected readonly drawingMode = signal<MapInteractionMode>('idle');
  protected readonly unsavedGeometry = signal<GeoJsonPolygon | null>(null);
  protected readonly hasUnsavedGeometry = computed(
    () => this.unsavedGeometry() !== null,
  );
  protected readonly savingState = signal<{
    siteId: string;
    action: SavingAction;
  } | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly createModalOpen = signal(false);
  protected readonly importWizardOpen = signal(false);
  protected readonly pendingBoundary = signal<GeoJsonPolygon | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly apiErrorState = signal<string | null>(null);
  protected readonly siteSummary = signal<SiteSummary | null>(null);
  protected readonly summaryLoading = signal(false);
  protected readonly summaryErrorMessage = signal<string | null>(null);
  protected readonly createErrorMessage = signal<string | null>(null);
  protected readonly hiddenSiteIds = signal<ReadonlySet<string>>(new Set());
  protected readonly activeInspectorTab = signal<InspectorTab>('overview');
  protected readonly renaming = signal(false);
  protected readonly scoringScenarios = signal<ScoringScenario[]>([]);
  protected readonly savedComparisons = signal<ComparisonSummary[]>([]);
  protected readonly comparisonMode = signal(false);
  protected readonly comparisonSiteIds = signal<ReadonlySet<string>>(new Set());
  protected readonly comparisonScenarioId = signal<string | null>(null);
  protected readonly comparisonMetricKeys = signal<ReadonlySet<string>>(
    new Set(),
  );
  protected readonly comparisonSaving = signal(false);
  protected readonly projectExporting = signal(false);
  protected readonly comparisonSites = computed(() => {
    const selectedIds = this.comparisonSiteIds();
    return this.sites().filter((site) => selectedIds.has(site.id));
  });
  protected readonly comparisonScenario = computed(
    () =>
      this.scoringScenarios().find(
        (scenario) => scenario.id === this.comparisonScenarioId(),
      ) ?? null,
  );

  protected readonly mapFeatures = computed<MapFeature[]>(() => {
    const hiddenIds = this.hiddenSiteIds();
    const editingSiteId = this.editingSiteId();
    const unsavedGeometry = this.unsavedGeometry();
    const comparisonIds = this.comparisonSiteIds();

    return this.sites().map((site) => ({
      id: site.id,
      geometry:
        site.id === editingSiteId && unsavedGeometry
          ? unsavedGeometry
          : site.boundary,
      visible: !hiddenIds.has(site.id),
      properties: {
        name: site.name,
        createdAtUtc: site.createdAtUtc,
        comparisonSelected: comparisonIds.has(site.id),
        status: site.status,
      },
    }));
  });

  protected readonly siteForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  protected readonly renameControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(200)],
  });

  ngOnInit(): void {
    this.destroyRef.onDestroy(() => {
      if (this.layerPreferencesSaveTimer) {
        clearTimeout(this.layerPreferencesSaveTimer);
      }
    });

    this.route.paramMap
      .pipe(
        map((params) => params.get('projectId') ?? ''),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((projectId) => this.loadWorkspace(projectId));
  }

  @HostListener('window:beforeunload', ['$event'])
  protected warnBeforeBrowserUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedGeometry()) {
      event.preventDefault();
      event.returnValue = true;
    }
  }

  public canDeactivate(): boolean {
    return (
      !this.hasUnsavedGeometry() ||
      window.confirm(
        'You have unsaved boundary changes. Leave this page and discard them?',
      )
    );
  }

  protected startDrawing(): void {
    if (!this.confirmDiscardGeometry('Start drawing a new site')) {
      return;
    }

    this.discardBoundaryDraft();
    this.createErrorMessage.set(null);
    this.pendingBoundary.set(null);
    this.createModalOpen.set(false);
    this.selectedSiteId.set(null);
    this.mapComponent?.startDrawing();
  }

  protected openImportWizard(): void {
    if (!this.confirmDiscardGeometry('Open the boundary importer')) {
      return;
    }

    this.discardBoundaryDraft();
    this.importWizardOpen.set(true);
  }

  protected closeImportWizard(): void {
    this.importWizardOpen.set(false);
  }

  protected handleImportCompleted(event: SiteImportCompletedEvent): void {
    const project = this.project();

    if (
      !project ||
      event.targetProjectId !== project.id ||
      event.result.importedSites.length === 0
    ) {
      return;
    }

    const importedIds = new Set(
      event.result.importedSites.map((site) => site.id),
    );
    this.sites.update((sites) => [
      ...event.result.importedSites,
      ...sites.filter((site) => !importedIds.has(site.id)),
    ]);
    this.project.update((currentProject) =>
      currentProject
        ? {
            ...currentProject,
            siteCount:
              currentProject.siteCount + event.result.importedSites.length,
          }
        : currentProject,
    );
    const firstImportedSite = event.result.importedSites[0];
    this.selectedSiteId.set(firstImportedSite.id);
    this.loadSiteSummary(firstImportedSite.id);
    setTimeout(() =>
      this.mapComponent?.fitToGeometry(firstImportedSite.boundary),
    );
  }

  protected handleGeometryCreated(event: MapGeometryEvent): void {
    this.pendingBoundary.set(event.geometry);
    this.siteForm.reset({ name: '' });
    this.createErrorMessage.set(null);
    this.createModalOpen.set(true);
  }

  protected closeCreateModal(): void {
    if (this.submitting()) {
      return;
    }

    this.createModalOpen.set(false);
    this.pendingBoundary.set(null);
    this.createErrorMessage.set(null);
    this.mapComponent?.clearDraft();
  }

  protected createSite(): void {
    const project = this.project();
    const boundary = this.pendingBoundary();

    if (!project || !boundary || this.siteForm.invalid || this.submitting()) {
      this.siteForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.createErrorMessage.set(null);

    this.sitesApi
      .createSite(project.id, {
        name: this.siteForm.controls.name.value.trim(),
        boundary,
      })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (site) => {
          this.sites.update((sites) => [site, ...sites]);
          this.project.update((currentProject) =>
            currentProject
              ? { ...currentProject, siteCount: currentProject.siteCount + 1 }
              : currentProject,
          );
          this.selectedSiteId.set(site.id);
          this.loadSiteSummary(site.id);
          this.siteForm.reset({ name: '' });
          this.pendingBoundary.set(null);
          this.createModalOpen.set(false);
          this.mapComponent?.clearDraft();
          setTimeout(() => this.mapComponent?.fitToGeometry(site.boundary));
        },
        error: (error: unknown) => {
          this.createErrorMessage.set(
            getApiErrorMessage(error, 'The site could not be created.'),
          );
        },
      });
  }

  protected startComparisonMode(): void {
    if (this.comparisonMode()) {
      this.cancelComparisonMode();
      return;
    }

    if (!this.confirmDiscardGeometry('Start a site comparison')) {
      return;
    }

    this.discardBoundaryDraft();
    const initiallySelected = this.selectedSiteId();
    this.comparisonSiteIds.set(
      new Set(initiallySelected ? [initiallySelected] : []),
    );
    this.selectedSiteId.set(null);
    this.siteSummary.set(null);
    this.comparisonMode.set(true);
    this.apiErrorState.set(null);

    const scenario = this.scoringScenarios()[0];
    if (scenario) {
      this.selectComparisonScenario(scenario.id);
    } else {
      this.comparisonScenarioId.set(null);
      this.comparisonMetricKeys.set(new Set());
      this.apiErrorState.set(
        'Save a scoring scenario from any site before comparing candidates.',
      );
    }
  }

  protected cancelComparisonMode(): void {
    this.comparisonMode.set(false);
    this.comparisonSiteIds.set(new Set());
    this.comparisonScenarioId.set(null);
    this.comparisonMetricKeys.set(new Set());
  }

  protected toggleComparisonSite(site: Site): void {
    const selected = this.comparisonSiteIds();

    if (!selected.has(site.id) && selected.size >= 5) {
      this.apiErrorState.set(
        'A comparison can include no more than five sites.',
      );
      return;
    }

    this.comparisonSiteIds.update((current) => {
      const next = new Set(current);
      if (next.has(site.id)) {
        next.delete(site.id);
      } else {
        next.add(site.id);
      }
      return next;
    });
    this.apiErrorState.set(null);
  }

  protected removeComparisonSite(siteId: string): void {
    this.comparisonSiteIds.update((current) => {
      const next = new Set(current);
      next.delete(siteId);
      return next;
    });
  }

  protected selectComparisonScenario(scenarioId: string): void {
    const scenario = this.scoringScenarios().find(
      (candidate) => candidate.id === scenarioId,
    );
    if (!scenario) return;

    this.comparisonScenarioId.set(scenario.id);
    this.comparisonMetricKeys.set(
      new Set(
        [...scenario.latestModel.criteria]
          .sort((left, right) => left.sortOrder - right.sortOrder)
          .map((criterion) => criterion.key),
      ),
    );
  }

  protected toggleComparisonMetric(metricKey: string, checked: boolean): void {
    this.comparisonMetricKeys.update((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(metricKey);
      } else {
        next.delete(metricKey);
      }
      return next;
    });
  }

  protected saveComparison(): void {
    const project = this.project();
    const scenarioId = this.comparisonScenarioId();

    if (
      !project ||
      !scenarioId ||
      this.comparisonSiteIds().size < 2 ||
      this.comparisonSiteIds().size > 5 ||
      this.comparisonMetricKeys().size === 0 ||
      this.comparisonSaving()
    ) {
      return;
    }

    this.comparisonSaving.set(true);
    this.apiErrorState.set(null);
    this.comparisonsApi
      .createComparison(project.id, {
        scoringScenarioId: scenarioId,
        siteIds: [...this.comparisonSiteIds()],
        metricKeys: [...this.comparisonMetricKeys()],
      })
      .pipe(finalize(() => this.comparisonSaving.set(false)))
      .subscribe({
        next: (comparison) => {
          void this.router.navigate(['/comparisons', comparison.id]);
        },
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(
              error,
              'The comparison could not be calculated and saved.',
            ),
          );
        },
      });
  }

  protected openSavedComparison(comparisonId: string): void {
    if (comparisonId) {
      void this.router.navigate(['/comparisons', comparisonId]);
    }
  }

  protected selectSite(site: Site): void {
    if (this.comparisonMode()) {
      this.toggleComparisonSite(site);
      return;
    }

    if (
      site.id !== this.selectedSiteId() &&
      !this.confirmDiscardGeometry(`Select "${site.name}"`)
    ) {
      return;
    }

    if (site.id !== this.selectedSiteId()) {
      this.discardBoundaryDraft();
      this.renaming.set(false);
      this.activeInspectorTab.set('overview');
      this.analysisOverlays.set([]);
      this.mapComponent?.clearHighlightedPosition();
    }

    if (this.hiddenSiteIds().has(site.id)) {
      this.hiddenSiteIds.update((ids) => {
        const nextIds = new Set(ids);
        nextIds.delete(site.id);
        return nextIds;
      });
    }

    this.selectedSiteId.set(site.id);
    this.loadSiteSummary(site.id);
    setTimeout(() => this.mapComponent?.fitToGeometry(site.boundary));
  }

  protected handleMapSelection(event: MapFeatureSelectionEvent): void {
    const site = this.sites().find(
      (candidate) => candidate.id === event.featureId,
    );

    if (site) {
      this.selectSite(site);
    }
  }

  protected handleGeometryEdited(event: MapGeometryEvent): void {
    if (!event.featureId) {
      return;
    }

    if (this.editingSiteId() !== event.featureId) {
      this.editingSiteId.set(event.featureId);
    }

    this.unsavedGeometry.set(this.cloneBoundary(event.geometry));
    this.activeInspectorTab.set('boundary');
  }

  protected handleGeometryDeleted(event: MapFeatureSelectionEvent): void {
    const site = this.sites().find(
      (candidate) => candidate.id === event.featureId,
    );

    if (site) {
      this.deleteSite(site);
    }
  }

  protected toggleSiteVisibility(site: Site): void {
    this.hiddenSiteIds.update((ids) => {
      const nextIds = new Set(ids);

      if (nextIds.has(site.id)) {
        nextIds.delete(site.id);
      } else {
        nextIds.add(site.id);
      }

      return nextIds;
    });
  }

  protected handleMapModeChanged(mode: MapInteractionMode): void {
    this.drawingMode.set(mode);

    if (mode !== 'editing') {
      return;
    }

    const site = this.selectedSite();

    if (!site) {
      return;
    }

    if (site.status === 'Archived') {
      this.apiErrorState.set('Restore this site before editing its boundary.');
      this.mapComponent?.stopEditing();
      return;
    }

    this.editingSiteId.set(site.id);
    this.activeInspectorTab.set('boundary');
  }

  protected handleOverlayStateChanged(change: MapOverlayStateChange): void {
    if (
      this.analysisOverlays().some((overlay) => overlay.id === change.layerId)
    ) {
      this.analysisOverlays.update((overlays) =>
        overlays.map((overlay) =>
          overlay.id === change.layerId
            ? {
                ...overlay,
                visible: change.visible,
                opacity: change.opacity,
                sortOrder: change.sortOrder,
                filter: change.filter,
              }
            : overlay,
        ),
      );
      return;
    }

    this.layerOverlays.update((overlays) =>
      overlays.map((overlay) =>
        overlay.id === change.layerId
          ? {
              ...overlay,
              visible: change.visible,
              opacity: change.opacity,
              sortOrder: change.sortOrder,
              filter: change.filter,
            }
          : overlay,
      ),
    );
    this.scheduleLayerPreferenceSave();
  }

  protected handleAnalysisOverlaysChanged(overlays: MapOverlay[]): void {
    this.analysisOverlays.set(overlays);
  }

  protected handleResultLayerRequested(result: AnalysisEvidenceResult): void {
    const site = this.selectedSite();
    if (site && result.resultGeometry) {
      this.mapComponent?.fitToGeometry(site.boundary, {
        padding: 96,
        maxZoom: 16,
      });
    }
  }

  protected selectInspectorTab(tab: InspectorTab): void {
    this.activeInspectorTab.set(tab);

    const site = this.selectedSite();

    if (tab === 'overview' && site && !this.siteSummary()) {
      this.loadSiteSummary(site.id);
    }
  }

  protected retrySiteSummary(siteId: string): void {
    this.loadSiteSummary(siteId);
  }

  protected highlightCentroid(summary: SiteSummary): void {
    const centroid = summary.location.centroid;

    if (!centroid) {
      return;
    }

    this.mapComponent?.highlightPosition([
      centroid.longitude,
      centroid.latitude,
    ]);
  }

  protected startRenaming(site: Site): void {
    this.renameControl.setValue(site.name);
    this.renameControl.markAsUntouched();
    this.renaming.set(true);
    this.apiErrorState.set(null);
  }

  protected cancelRenaming(): void {
    this.renaming.set(false);
    this.renameControl.reset('');
  }

  protected saveRename(site: Site): void {
    if (this.renameControl.invalid || this.isSaving(site.id)) {
      this.renameControl.markAsTouched();
      return;
    }

    const name = this.renameControl.value.trim();

    if (name === site.name) {
      this.cancelRenaming();
      return;
    }

    this.beginSaving(site.id, 'rename');

    this.sitesApi
      .updateSite(site.id, { name })
      .pipe(finalize(() => this.endSaving()))
      .subscribe({
        next: (updatedSite) => {
          this.replaceSite(updatedSite);
          this.renaming.set(false);
        },
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(error, 'The site could not be renamed.'),
          );
        },
      });
  }

  protected startBoundaryEditing(site: Site): void {
    if (site.status === 'Archived') {
      this.apiErrorState.set('Restore this site before editing its boundary.');
      return;
    }

    if (
      this.editingSiteId() &&
      this.editingSiteId() !== site.id &&
      !this.confirmDiscardGeometry(`Edit "${site.name}"`)
    ) {
      return;
    }

    this.discardBoundaryDraft();
    this.selectedSiteId.set(site.id);
    this.editingSiteId.set(site.id);
    this.apiErrorState.set(null);
    setTimeout(() => this.mapComponent?.editSelected());
  }

  protected cancelBoundaryEditing(): void {
    if (
      this.hasUnsavedGeometry() &&
      !window.confirm('Discard the unsaved boundary changes?')
    ) {
      return;
    }

    this.discardBoundaryDraft();
  }

  protected saveBoundaryChanges(): void {
    const site = this.editingSite();
    const boundary = this.unsavedGeometry();

    if (!site || !boundary || this.isSaving(site.id)) {
      return;
    }

    this.beginSaving(site.id, 'boundary');

    this.sitesApi
      .updateBoundary(site.id, { boundary })
      .pipe(finalize(() => this.endSaving()))
      .subscribe({
        next: (updatedSite) => {
          this.replaceSite(updatedSite);
          this.unsavedGeometry.set(null);
          this.editingSiteId.set(null);
          this.mapComponent?.clearHighlightedPosition();
          this.mapComponent?.stopEditing();
          this.loadSiteSummary(updatedSite.id);
        },
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(
              error,
              'The boundary changes could not be saved.',
            ),
          );
        },
      });
  }

  protected archiveSite(site: Site): void {
    if (
      this.isSaving(site.id) ||
      !this.confirmDiscardGeometry(`Archive "${site.name}"`) ||
      !window.confirm(
        `Archive "${site.name}"? It will remain available and can be restored.`,
      )
    ) {
      return;
    }

    this.discardBoundaryDraft();
    this.beginSaving(site.id, 'archive');

    this.sitesApi
      .archiveSite(site.id)
      .pipe(finalize(() => this.endSaving()))
      .subscribe({
        next: (updatedSite) => this.replaceSite(updatedSite),
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(error, 'The site could not be archived.'),
          );
        },
      });
  }

  protected restoreSite(site: Site): void {
    if (this.isSaving(site.id)) {
      return;
    }

    this.beginSaving(site.id, 'restore');

    this.sitesApi
      .restoreSite(site.id)
      .pipe(finalize(() => this.endSaving()))
      .subscribe({
        next: (updatedSite) => this.replaceSite(updatedSite),
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(error, 'The site could not be restored.'),
          );
        },
      });
  }

  protected deleteSite(site: Site): void {
    if (
      this.isSaving(site.id) ||
      !this.confirmDiscardGeometry(`Delete "${site.name}"`) ||
      !window.confirm(
        `Delete "${site.name}"? This permanently removes its boundary.`,
      )
    ) {
      return;
    }

    this.discardBoundaryDraft();
    this.beginSaving(site.id, 'delete');

    this.sitesApi
      .deleteSite(site.id)
      .pipe(finalize(() => this.endSaving()))
      .subscribe({
        next: () => {
          this.sites.update((sites) =>
            sites.filter((candidate) => candidate.id !== site.id),
          );
          this.hiddenSiteIds.update((ids) => {
            const nextIds = new Set(ids);
            nextIds.delete(site.id);
            return nextIds;
          });
          this.removeComparisonSite(site.id);
          this.project.update((currentProject) =>
            currentProject
              ? {
                  ...currentProject,
                  siteCount: Math.max(0, currentProject.siteCount - 1),
                }
              : currentProject,
          );

          if (this.selectedSiteId() === site.id) {
            this.selectedSiteId.set(null);
            this.siteSummary.set(null);
            this.summaryErrorMessage.set(null);
            this.mapComponent?.clearHighlightedPosition();
          }
        },
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(error, 'The site could not be deleted.'),
          );
        },
      });
  }

  protected downloadGeoJson(site: Site): void {
    this.apiErrorState.set(null);

    this.sitesApi.exportSiteGeoJson(site.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        const filename = site.name
          .toLocaleLowerCase()
          .replace(/[^a-z0-9]+/g, '-')
          .replace(/^-|-$/g, '');

        link.href = url;
        link.download = `${filename || 'site'}-boundary.geojson`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (error: unknown) => {
        this.apiErrorState.set(
          getApiErrorMessage(error, 'The Site could not be exported.'),
        );
      },
    });
  }

  protected exportProjectArchive(): void {
    const project = this.project();
    if (!project || this.projectExporting()) {
      return;
    }

    this.projectExporting.set(true);
    this.apiErrorState.set(null);
    this.reportsApi
      .createProjectArchive(project.id, {
        title: `${project.name} project data archive`,
        format: 'ProjectArchive',
        sections: [],
        branding: {
          organizationName: 'FormaGeo',
          preparedBy: 'FormaGeo analysis team',
          accentColor: '#0F766E',
          footerText: 'Prepared with FormaGeo spatial decision support',
        },
      })
      .pipe(finalize(() => this.projectExporting.set(false)))
      .subscribe({
        next: (report) => {
          this.reportsApi.downloadReport(report.id).subscribe({
            next: (blob) => {
              const url = URL.createObjectURL(blob);
              const link = document.createElement('a');
              link.href = url;
              link.download = report.fileName;
              link.click();
              URL.revokeObjectURL(url);
            },
            error: (error: unknown) => {
              this.apiErrorState.set(
                getApiErrorMessage(
                  error,
                  'The project archive could not be downloaded.',
                ),
              );
            },
          });
        },
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(
              error,
              'The project archive could not be generated.',
            ),
          );
        },
      });
  }

  protected isSaving(siteId: string, action?: SavingAction): boolean {
    const state = this.savingState();
    return (
      state?.siteId === siteId &&
      (action === undefined || state.action === action)
    );
  }

  protected retry(): void {
    const projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
    this.loadWorkspace(projectId);
  }

  protected positionCount(site: Site): number {
    return site.boundary.coordinates.reduce(
      (total, ring) => total + ring.length,
      0,
    );
  }

  private loadWorkspace(projectId: string): void {
    if (!projectId) {
      this.loading.set(false);
      this.errorMessage.set('No project ID was provided.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.project.set(null);
    this.sites.set([]);
    this.layerOverlays.set([]);
    this.analysisOverlays.set([]);
    this.selectedSiteId.set(null);
    this.siteSummary.set(null);
    this.summaryLoading.set(false);
    this.summaryErrorMessage.set(null);
    this.summaryRequestSequence += 1;
    this.discardBoundaryDraft();
    this.hiddenSiteIds.set(new Set());
    this.comparisonMode.set(false);
    this.comparisonSiteIds.set(new Set());
    this.comparisonScenarioId.set(null);
    this.comparisonMetricKeys.set(new Set());
    this.scoringScenarios.set([]);
    this.savedComparisons.set([]);

    forkJoin({
      project: this.projectsApi.getProject(projectId),
      sites: this.sitesApi.getProjectSites(projectId),
      layers: this.layersApi.getLayers(),
      projectLayers: this.layersApi.getProjectLayers(projectId),
      scoringScenarios: this.scoringApi.getProjectScenarios(projectId),
      savedComparisons: this.comparisonsApi.getProjectComparisons(projectId),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({
          project,
          sites,
          layers,
          projectLayers,
          scoringScenarios,
          savedComparisons,
        }) => {
          this.project.set(project);
          this.sites.set(sites);
          this.scoringScenarios.set(scoringScenarios);
          this.savedComparisons.set(savedComparisons);
          this.layerOverlays.set(this.createMapOverlays(layers, projectLayers));
          this.loadLayerLegends(layers);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'The project workspace could not be loaded.',
            ),
          );
        },
      });
  }

  private confirmDiscardGeometry(nextAction: string): boolean {
    return (
      !this.hasUnsavedGeometry() ||
      window.confirm(
        `${nextAction}? Your unsaved boundary changes will be discarded.`,
      )
    );
  }

  private discardBoundaryDraft(): void {
    this.unsavedGeometry.set(null);
    this.editingSiteId.set(null);
    this.mapComponent?.stopEditing();
  }

  private beginSaving(siteId: string, action: SavingAction): void {
    this.apiErrorState.set(null);
    this.savingState.set({ siteId, action });
  }

  private endSaving(): void {
    this.savingState.set(null);
  }

  private replaceSite(updatedSite: Site): void {
    this.sites.update((sites) =>
      sites.map((site) => (site.id === updatedSite.id ? updatedSite : site)),
    );
  }

  private loadSiteSummary(siteId: string): void {
    const requestSequence = ++this.summaryRequestSequence;

    this.siteSummary.set(null);
    this.summaryLoading.set(true);
    this.summaryErrorMessage.set(null);

    this.sitesApi
      .getSiteSummary(siteId)
      .pipe(
        finalize(() => {
          if (requestSequence === this.summaryRequestSequence) {
            this.summaryLoading.set(false);
          }
        }),
      )
      .subscribe({
        next: (summary) => {
          if (
            requestSequence === this.summaryRequestSequence &&
            this.selectedSiteId() === siteId
          ) {
            this.siteSummary.set(summary);
          }
        },
        error: (error: unknown) => {
          if (
            requestSequence === this.summaryRequestSequence &&
            this.selectedSiteId() === siteId
          ) {
            this.summaryErrorMessage.set(
              getApiErrorMessage(
                error,
                'The site measurements could not be loaded.',
              ),
            );
          }
        },
      });
  }

  private cloneBoundary(boundary: GeoJsonPolygon): GeoJsonPolygon {
    return {
      type: 'Polygon',
      coordinates: boundary.coordinates.map((ring) =>
        ring.map(([longitude, latitude]) => [longitude, latitude]),
      ),
    };
  }

  private createMapOverlays(
    layers: LayerDefinition[],
    preferences: ProjectLayerPreference[],
  ): MapOverlay[] {
    const preferencesByLayerId = new Map(
      preferences.map((preference) => [preference.layerId, preference]),
    );

    return layers.map((layer, index) => {
      const preference = preferencesByLayerId.get(layer.id);

      return {
        id: layer.id,
        name: layer.name,
        description: layer.description,
        category: layer.category,
        geographicCoverage: layer.geographicCoverage,
        coordinateSystem: layer.coordinateSystem,
        geometryType: layer.geometryType,
        featureNameProperty: layer.featureNameProperty,
        style: layer.style,
        dataSource: layer.dataSource,
        lastUpdatedAtUtc: layer.version.lastUpdatedAtUtc,
        deliveryMethod: layer.version.deliveryMethod,
        dataUrl: layer.version.dataUrl,
        sourceLayer: layer.version.sourceLayer,
        minimumZoom: layer.version.minimumZoom,
        maximumZoom: layer.version.maximumZoom,
        legend: [],
        visible: preference?.isVisible ?? false,
        opacity: preference?.opacity ?? 0.8,
        sortOrder: preference?.sortOrder ?? index,
        filter: preference?.filter ?? null,
      };
    });
  }

  private loadLayerLegends(layers: LayerDefinition[]): void {
    if (layers.length === 0) {
      return;
    }

    forkJoin(layers.map((layer) => this.layersApi.getLegend(layer.id)))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (legends) => {
          const legendsByLayerId = new Map(
            legends.map((legend) => [legend.layerId, legend.items]),
          );
          this.layerOverlays.update((overlays) =>
            overlays.map((overlay) => ({
              ...overlay,
              legend: legendsByLayerId.get(overlay.id) ?? [],
            })),
          );
        },
        error: () => {
          this.apiErrorState.set(
            'One or more layer legends could not be loaded.',
          );
        },
      });
  }

  private scheduleLayerPreferenceSave(): void {
    if (this.layerPreferencesSaveTimer) {
      clearTimeout(this.layerPreferencesSaveTimer);
    }

    this.layerPreferencesSaveTimer = setTimeout(
      () => this.saveLayerPreferences(),
      300,
    );
  }

  private saveLayerPreferences(): void {
    const project = this.project();

    if (!project) {
      return;
    }

    const layers = this.layerOverlays().map((overlay) => ({
      layerId: overlay.id,
      isVisible: overlay.visible,
      opacity: overlay.opacity,
      sortOrder: overlay.sortOrder,
      filter: overlay.filter,
    }));

    this.layersApi
      .updateProjectLayers(project.id, { layers })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: (error: unknown) => {
          this.apiErrorState.set(
            getApiErrorMessage(error, 'Layer preferences could not be saved.'),
          );
        },
      });
  }
}
