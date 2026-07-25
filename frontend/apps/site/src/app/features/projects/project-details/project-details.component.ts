import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
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
  GeoJsonPolygon,
  GeoJsonPosition,
  Project,
  Site,
} from '@frontend/models';
import { forkJoin, finalize } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

const DEFAULT_BOUNDARY = `{
  "type": "Polygon",
  "coordinates": [
    [
      [121.0301, 14.6501],
      [121.0312, 14.6501],
      [121.0312, 14.6512],
      [121.0301, 14.6512],
      [121.0301, 14.6501]
    ]
  ]
}`;

@Component({
  selector: 'fg-project-details',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './project-details.component.html',
  styleUrl: './project-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailsComponent implements OnInit {
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
  protected readonly createPanelOpen = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly createErrorMessage = signal<string | null>(null);

  protected readonly siteForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    boundary: new FormControl(DEFAULT_BOUNDARY, {
      nonNullable: true,
      validators: [Validators.required],
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

  protected selectSite(site: Site): void {
    this.selectedSite.set(site);
  }

  protected openCreatePanel(): void {
    this.createErrorMessage.set(null);
    this.createPanelOpen.set(true);
  }

  protected closeCreatePanel(): void {
    if (this.submitting()) {
      return;
    }

    this.createPanelOpen.set(false);
    this.createErrorMessage.set(null);
  }

  protected createSite(): void {
    const project = this.project();

    if (!project || this.siteForm.invalid || this.submitting()) {
      this.siteForm.markAllAsTouched();
      return;
    }

    const boundary = this.parseBoundary(this.siteForm.controls.boundary.value);

    if (!boundary) {
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
          this.siteForm.reset({
            name: '',
            boundary: DEFAULT_BOUNDARY,
          });
          this.createPanelOpen.set(false);
        },
        error: (error: unknown) => {
          this.createErrorMessage.set(
            getApiErrorMessage(error, 'The site could not be created.'),
          );
        },
      });
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

  private parseBoundary(value: string): GeoJsonPolygon | null {
    let candidate: unknown;

    try {
      candidate = JSON.parse(value);
    } catch {
      this.createErrorMessage.set(
        'Boundary must be valid JSON. Check commas, quotes, and brackets.',
      );
      return null;
    }

    if (!this.isGeoJsonPolygon(candidate)) {
      this.createErrorMessage.set(
        'Enter a GeoJSON Polygon with closed rings and [longitude, latitude] positions.',
      );
      return null;
    }

    return candidate;
  }

  private isGeoJsonPolygon(value: unknown): value is GeoJsonPolygon {
    if (
      typeof value !== 'object' ||
      value === null ||
      !('type' in value) ||
      value.type !== 'Polygon' ||
      !('coordinates' in value) ||
      !Array.isArray(value.coordinates) ||
      value.coordinates.length === 0
    ) {
      return false;
    }

    return value.coordinates.every((ring) => {
      if (!Array.isArray(ring) || ring.length < 4) {
        return false;
      }

      const positionsAreValid = ring.every(
        (position): position is GeoJsonPosition =>
          Array.isArray(position) &&
          position.length === 2 &&
          position.every(
            (coordinate) =>
              typeof coordinate === 'number' && Number.isFinite(coordinate),
          ) &&
          position[0] >= -180 &&
          position[0] <= 180 &&
          position[1] >= -90 &&
          position[1] <= 90,
      );

      if (!positionsAreValid) {
        return false;
      }

      const firstPosition = ring[0];
      const lastPosition = ring[ring.length - 1];

      return (
        firstPosition[0] === lastPosition[0] &&
        firstPosition[1] === lastPosition[1]
      );
    });
  }
}
