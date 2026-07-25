import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
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
} from '@frontend/map';
import { GeoJsonPolygon, Project, Site } from '@frontend/models';
import { finalize, forkJoin } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';

@Component({
  selector: 'fg-project-details',
  standalone: true,
  imports: [CommonModule, MapComponent, ReactiveFormsModule, RouterLink],
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
  protected readonly selectedSite = signal<Site | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly deletingSiteId = signal<string | null>(null);
  protected readonly createModalOpen = signal(false);
  protected readonly pendingBoundary = signal<GeoJsonPolygon | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly createErrorMessage = signal<string | null>(null);
  protected readonly searchQuery = signal('');
  protected readonly hiddenSiteIds = signal<ReadonlySet<string>>(new Set());

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

    return this.sites().map((site) => ({
      id: site.id,
      geometry: site.boundary,
      visible: !hiddenIds.has(site.id),
      properties: {
        name: site.name,
        createdAtUtc: site.createdAtUtc,
      },
    }));
  });

  protected readonly siteForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
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

  protected startDrawing(): void {
    this.createErrorMessage.set(null);
    this.pendingBoundary.set(null);
    this.createModalOpen.set(false);
    this.selectedSite.set(null);
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
          this.selectedSite.set(site);
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
    if (this.hiddenSiteIds().has(site.id)) {
      this.hiddenSiteIds.update((ids) => {
        const nextIds = new Set(ids);
        nextIds.delete(site.id);
        return nextIds;
      });
    }

    this.selectedSite.set(site);
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

    this.sites.update((sites) =>
      sites.map((site) =>
        site.id === event.featureId
          ? { ...site, boundary: event.geometry }
          : site,
      ),
    );

    const selected = this.selectedSite();
    if (selected?.id === event.featureId) {
      this.selectedSite.set({
        ...selected,
        boundary: event.geometry,
      });
    }
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

  protected deleteSite(site: Site): void {
    if (
      this.deletingSiteId() ||
      !window.confirm(
        `Delete "${site.name}"? This permanently removes its boundary.`,
      )
    ) {
      return;
    }

    this.deletingSiteId.set(site.id);
    this.errorMessage.set(null);

    this.sitesApi
      .deleteSite(site.id)
      .pipe(finalize(() => this.deletingSiteId.set(null)))
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

          if (this.selectedSite()?.id === site.id) {
            this.selectedSite.set(null);
          }
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The site could not be deleted.'),
          );
        },
      });
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
    this.selectedSite.set(null);
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
}
