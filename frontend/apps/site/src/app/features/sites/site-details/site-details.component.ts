import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { getApiErrorMessage, SitesApiService } from '@frontend/api-client';
import { AnalysisEvidenceResult, Site } from '@frontend/models';
import {
  MapComponent,
  MapFeature,
  MapOverlay,
  MapOverlayStateChange,
} from '@frontend/map';
import {
  AlertComponent,
  PageStateComponent,
  StatusBadgeComponent,
} from '@frontend/ui';
import {
  LucideArrowLeft,
  LucideChartNoAxesCombined,
  LucideTrash2,
} from '@lucide/angular';
import { finalize } from 'rxjs';
import { SiteAnalysisComponent } from '../../analyses/site-analysis.component';

@Component({
  selector: 'fg-site-details',
  standalone: true,
  imports: [
    AlertComponent,
    CommonModule,
    LucideArrowLeft,
    LucideChartNoAxesCombined,
    LucideTrash2,
    MapComponent,
    PageStateComponent,
    RouterLink,
    SiteAnalysisComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './site-details.component.html',
  styleUrl: './site-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteDetailsComponent implements OnInit {
  @ViewChild(MapComponent)
  private mapComponent?: MapComponent;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sitesApi = inject(SitesApiService);

  protected readonly site = signal<Site | null>(null);
  protected readonly loading = signal(true);
  protected readonly deleting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly analysisOverlays = signal<MapOverlay[]>([]);
  protected readonly mapFeatures = computed<MapFeature[]>(() => {
    const site = this.site();
    return site
      ? [
          {
            id: site.id,
            geometry: site.boundary,
            properties: {
              name: site.name,
              status: site.status,
            },
          },
        ]
      : [];
  });

  ngOnInit(): void {
    this.loadSite();
  }

  protected deleteSite(): void {
    const site = this.site();

    if (
      !site ||
      this.deleting() ||
      !window.confirm(
        `Delete "${site.name}"? This permanently removes its boundary.`,
      )
    ) {
      return;
    }

    this.deleting.set(true);
    this.errorMessage.set(null);

    this.sitesApi
      .deleteSite(site.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/projects', site.projectId]);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The site could not be deleted.'),
          );
        },
      });
  }

  protected retry(): void {
    this.loadSite();
  }

  protected formattedBoundary(site: Site): string {
    return JSON.stringify(site.boundary, null, 2);
  }

  protected handleAnalysisOverlaysChanged(overlays: MapOverlay[]): void {
    this.analysisOverlays.set(overlays);
  }

  protected handleOverlayStateChanged(change: MapOverlayStateChange): void {
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
  }

  protected handleResultLayerRequested(result: AnalysisEvidenceResult): void {
    const site = this.site();
    if (site && result.resultGeometry) {
      this.mapComponent?.fitToGeometry(site.boundary, {
        padding: 80,
        maxZoom: 16,
      });
    }
  }

  private loadSite(): void {
    const siteId = this.route.snapshot.paramMap.get('siteId');

    if (!siteId) {
      this.loading.set(false);
      this.errorMessage.set('No site ID was provided.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.site.set(null);
    this.analysisOverlays.set([]);

    this.sitesApi
      .getSite(siteId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (site) => this.site.set(site),
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The site could not be loaded.'),
          );
        },
      });
  }
}
