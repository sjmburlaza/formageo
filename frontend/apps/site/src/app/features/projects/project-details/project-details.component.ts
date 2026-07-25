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
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  getApiErrorMessage,
  ProjectsApiService,
  SitesApiService,
} from '@frontend/api-client';
import {
  MapComponent,
  MapFeature,
  MapFeatureSelectionEvent,
  MapGeometryEvent,
  MapInteractionMode,
} from '@frontend/map';
import { GeoJsonPolygon, Project, Site } from '@frontend/models';
import {
  LucideArrowLeft,
  LucideArrowRight,
  LucideChartNoAxesCombined,
  LucideCircleAlert,
  LucideCircleCheck,
  LucideColumns3,
  LucideEye,
  LucideEyeOff,
  LucideFileText,
  LucideLandPlot,
  LucidePenTool,
  LucideSearch,
  LucideUpload,
  LucideX,
} from '@lucide/angular';
import { finalize, forkJoin } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';

type InspectorTab = 'overview' | 'boundary' | 'analysis' | 'history';
type SavingAction = 'rename' | 'boundary' | 'archive' | 'restore' | 'delete';

@Component({
  selector: 'fg-project-details',
  standalone: true,
  imports: [
    CommonModule,
    LucideArrowLeft,
    LucideArrowRight,
    LucideChartNoAxesCombined,
    LucideCircleAlert,
    LucideCircleCheck,
    LucideColumns3,
    LucideEye,
    LucideEyeOff,
    LucideFileText,
    LucideLandPlot,
    LucidePenTool,
    LucideSearch,
    LucideUpload,
    LucideX,
    MapComponent,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './project-details.component.html',
  styleUrl: './project-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailsComponent implements OnInit {
  @ViewChild(MapComponent)
  private mapComponent?: MapComponent;

  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly projectsApi = inject(ProjectsApiService);
  private readonly sitesApi = inject(SitesApiService);

  protected readonly project = signal<Project | null>(null);
  protected readonly sites = signal<Site[]>([]);
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
  protected readonly pendingBoundary = signal<GeoJsonPolygon | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly apiErrorState = signal<string | null>(null);
  protected readonly createErrorMessage = signal<string | null>(null);
  protected readonly searchQuery = signal('');
  protected readonly hiddenSiteIds = signal<ReadonlySet<string>>(new Set());
  protected readonly activeInspectorTab = signal<InspectorTab>('overview');
  protected readonly renaming = signal(false);

  protected readonly filteredSites = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();

    if (!query) {
      return this.sites();
    }

    return this.sites().filter((site) =>
      site.name.toLocaleLowerCase().includes(query),
    );
  });

  protected readonly mapFeatures = computed<MapFeature[]>(() => {
    const hiddenIds = this.hiddenSiteIds();
    const editingSiteId = this.editingSiteId();
    const unsavedGeometry = this.unsavedGeometry();

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

  protected selectSite(site: Site): void {
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
    }

    if (this.hiddenSiteIds().has(site.id)) {
      this.hiddenSiteIds.update((ids) => {
        const nextIds = new Set(ids);
        nextIds.delete(site.id);
        return nextIds;
      });
    }

    this.selectedSiteId.set(site.id);
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

  protected toggleSiteVisibility(site: Site, event: Event): void {
    event.stopPropagation();
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

  protected updateSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
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

  protected selectInspectorTab(tab: InspectorTab): void {
    this.activeInspectorTab.set(tab);
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
          this.mapComponent?.stopEditing();
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
    const feature = {
      type: 'Feature',
      properties: {
        id: site.id,
        name: site.name,
        status: site.status,
        coordinateSystem: 'EPSG:4326',
      },
      geometry: site.boundary,
    };
    const blob = new Blob([JSON.stringify(feature, null, 2)], {
      type: 'application/geo+json',
    });
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
    this.selectedSiteId.set(null);
    this.discardBoundaryDraft();
    this.hiddenSiteIds.set(new Set());

    forkJoin({
      project: this.projectsApi.getProject(projectId),
      sites: this.sitesApi.getProjectSites(projectId),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ project, sites }) => {
          this.project.set(project);
          this.sites.set(sites);
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

  private cloneBoundary(boundary: GeoJsonPolygon): GeoJsonPolygon {
    return {
      type: 'Polygon',
      coordinates: boundary.coordinates.map((ring) =>
        ring.map(([longitude, latitude]) => [longitude, latitude]),
      ),
    };
  }
}
