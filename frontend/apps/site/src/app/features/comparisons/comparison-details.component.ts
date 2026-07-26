import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ComparisonsApiService,
  getApiErrorMessage,
  ProjectsApiService,
  SitesApiService,
} from '@frontend/api-client';
import { MapComponent, MapFeature } from '@frontend/map';
import {
  ComparisonMetric,
  ComparisonRanking,
  CriterionScore,
  Project,
  Site,
  SiteComparison,
} from '@frontend/models';
import { AlertComponent, PageStateComponent } from '@frontend/ui';
import {
  LucideArrowLeft,
  LucideArrowUpDown,
  LucideCheck,
  LucideCircleAlert,
  LucideDownload,
  LucideFileText,
  LucideMap,
  LucideSlidersHorizontal,
  LucideTrophy,
} from '@lucide/angular';
import { finalize, forkJoin, switchMap, tap } from 'rxjs';

type SortDirection = 'ascending' | 'descending';

@Component({
  selector: 'fg-comparison-details',
  standalone: true,
  imports: [
    AlertComponent,
    CommonModule,
    LucideArrowLeft,
    LucideArrowUpDown,
    LucideCheck,
    LucideCircleAlert,
    LucideDownload,
    LucideFileText,
    LucideMap,
    LucideSlidersHorizontal,
    LucideTrophy,
    MapComponent,
    PageStateComponent,
    RouterLink,
  ],
  templateUrl: './comparison-details.component.html',
  styleUrl: './comparison-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComparisonDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly comparisonsApi = inject(ComparisonsApiService);
  private readonly projectsApi = inject(ProjectsApiService);
  private readonly sitesApi = inject(SitesApiService);

  protected readonly comparison = signal<SiteComparison | null>(null);
  protected readonly project = signal<Project | null>(null);
  protected readonly sites = signal<Site[]>([]);
  protected readonly loading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly selectedSiteId = signal<string | null>(null);
  protected readonly selectedMetricKeys = signal<ReadonlySet<string>>(
    new Set(),
  );
  protected readonly sortMetricKey = signal('overall');
  protected readonly sortDirection = signal<SortDirection>('descending');

  protected readonly displayedMetrics = computed(() => {
    const comparison = this.comparison();
    const selectedKeys = this.selectedMetricKeys();
    return (
      comparison?.metrics.filter((metric) => selectedKeys.has(metric.key)) ?? []
    );
  });
  protected readonly sortedRankings = computed(() => {
    const rankings = [...(this.comparison()?.rankings ?? [])];
    const metricKey = this.sortMetricKey();
    const direction = this.sortDirection();

    return rankings.sort((left, right) => {
      const leftValue = this.sortValue(left, metricKey);
      const rightValue = this.sortValue(right, metricKey);

      if (leftValue === null && rightValue === null) {
        return left.rank - right.rank;
      }
      if (leftValue === null) return 1;
      if (rightValue === null) return -1;

      const difference = leftValue - rightValue;
      return direction === 'ascending' ? difference : -difference;
    });
  });
  protected readonly selectedRanking = computed(
    () =>
      this.comparison()?.rankings.find(
        (ranking) => ranking.siteId === this.selectedSiteId(),
      ) ??
      this.comparison()?.rankings[0] ??
      null,
  );
  protected readonly mapFeatures = computed<MapFeature[]>(() => {
    const selectedIds = new Set(this.comparison()?.selectedSiteIds ?? []);

    return this.sites()
      .filter((site) => selectedIds.has(site.id))
      .map((site) => ({
        id: site.id,
        geometry: site.boundary,
        properties: {
          name: site.name,
          comparisonSelected: true,
          status: site.status,
        },
      }));
  });

  ngOnInit(): void {
    const comparisonId = this.route.snapshot.paramMap.get('comparisonId');

    if (!comparisonId) {
      this.loading.set(false);
      this.errorMessage.set('No comparison ID was provided.');
      return;
    }

    this.comparisonsApi
      .getComparison(comparisonId)
      .pipe(
        tap((comparison) => {
          this.comparison.set(comparison);
          this.selectedMetricKeys.set(
            new Set(comparison.metrics.map((metric) => metric.key)),
          );
          this.selectedSiteId.set(comparison.rankings[0]?.siteId ?? null);
        }),
        switchMap((comparison) =>
          forkJoin({
            project: this.projectsApi.getProject(comparison.projectId),
            sites: this.sitesApi.getProjectSites(comparison.projectId),
          }),
        ),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: ({ project, sites }) => {
          this.project.set(project);
          this.sites.set(sites);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'The saved comparison could not be loaded.',
            ),
          );
        },
      });
  }

  protected selectSite(siteId: string): void {
    this.selectedSiteId.set(siteId);
  }

  protected toggleMetric(metricKey: string, checked: boolean): void {
    this.selectedMetricKeys.update((selected) => {
      const next = new Set(selected);
      if (checked) {
        next.add(metricKey);
      } else if (next.size > 1) {
        next.delete(metricKey);
      }
      return next;
    });

    if (
      !this.selectedMetricKeys().has(this.sortMetricKey()) &&
      this.sortMetricKey() !== 'overall'
    ) {
      this.sortMetricKey.set('overall');
      this.sortDirection.set('descending');
    }
  }

  protected sortBy(metricKey: string): void {
    if (this.sortMetricKey() === metricKey) {
      this.sortDirection.update((direction) =>
        direction === 'ascending' ? 'descending' : 'ascending',
      );
      return;
    }

    this.sortMetricKey.set(metricKey);
    const metric = this.comparison()?.metrics.find(
      (candidate) => candidate.key === metricKey,
    );
    this.sortDirection.set(
      metric?.direction === 'LowerIsBetter' ? 'ascending' : 'descending',
    );
  }

  protected sortAriaLabel(metricKey: string): string {
    const active = this.sortMetricKey() === metricKey;
    return active
      ? `Sorted ${this.sortDirection()}. Activate to reverse the order.`
      : 'Activate to sort by this column.';
  }

  protected metricFor(
    ranking: ComparisonRanking,
    metricKey: string,
  ): CriterionScore | undefined {
    return ranking.metrics.find((metric) => metric.criterionKey === metricKey);
  }

  protected formatValue(value: number | null, unit: string): string {
    if (value === null) return 'No data';

    const formatted = value.toLocaleString(undefined, {
      maximumFractionDigits: 2,
    });
    return unit === '%' ? `${formatted}%` : `${formatted} ${unit}`.trim();
  }

  protected isBest(
    ranking: ComparisonRanking,
    metric: ComparisonMetric,
  ): boolean {
    return this.isExtreme(ranking, metric, true);
  }

  protected isWorst(
    ranking: ComparisonRanking,
    metric: ComparisonMetric,
  ): boolean {
    return this.isExtreme(ranking, metric, false);
  }

  protected isBestScore(ranking: ComparisonRanking): boolean {
    const scores =
      this.comparison()
        ?.rankings.map((item) => item.overallScore)
        .filter((score): score is number => score !== null) ?? [];

    return (
      ranking.overallScore !== null &&
      new Set(scores).size > 1 &&
      ranking.overallScore === Math.max(...scores)
    );
  }

  protected isWorstScore(ranking: ComparisonRanking): boolean {
    const scores =
      this.comparison()
        ?.rankings.map((item) => item.overallScore)
        .filter((score): score is number => score !== null) ?? [];

    return (
      ranking.overallScore !== null &&
      new Set(scores).size > 1 &&
      ranking.overallScore === Math.min(...scores)
    );
  }

  protected scoreTone(score: number | null): string {
    if (score === null) return 'score--missing';
    if (score >= 80) return 'score--excellent';
    if (score >= 65) return 'score--good';
    if (score >= 50) return 'score--moderate';
    return 'score--weak';
  }

  protected exportCsv(): void {
    const comparison = this.comparison();
    if (!comparison) return;

    const metrics = this.displayedMetrics();
    const header = [
      'Rank',
      'Site',
      'Overall score',
      'Rating',
      ...metrics.map((metric) => `${metric.name} (${metric.unit || 'value'})`),
    ];
    const rows = comparison.rankings.map((ranking) => [
      ranking.rank,
      ranking.siteName,
      ranking.overallScore ?? '',
      ranking.rating,
      ...metrics.map(
        (metric) => this.metricFor(ranking, metric.key)?.rawValue ?? '',
      ),
    ]);
    const csv = [header, ...rows]
      .map((row) => row.map((value) => this.escapeCsv(String(value))).join(','))
      .join('\n');
    const blob = new Blob([csv], {
      type: 'text/csv;charset=utf-8',
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `site-comparison-${comparison.id}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private sortValue(
    ranking: ComparisonRanking,
    metricKey: string,
  ): number | null {
    return metricKey === 'overall'
      ? ranking.overallScore
      : (this.metricFor(ranking, metricKey)?.rawValue ?? null);
  }

  private isExtreme(
    ranking: ComparisonRanking,
    metric: ComparisonMetric,
    best: boolean,
  ): boolean {
    const values =
      this.comparison()
        ?.rankings.map(
          (item) => this.metricFor(item, metric.key)?.rawValue ?? null,
        )
        .filter((value): value is number => value !== null) ?? [];
    const value = this.metricFor(ranking, metric.key)?.rawValue;

    if (value === null || value === undefined || new Set(values).size <= 1) {
      return false;
    }

    const higherWins = metric.direction === 'HigherIsBetter';
    const target =
      best === higherWins ? Math.max(...values) : Math.min(...values);
    return value === target;
  }

  private escapeCsv(value: string): string {
    return /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
  }
}
