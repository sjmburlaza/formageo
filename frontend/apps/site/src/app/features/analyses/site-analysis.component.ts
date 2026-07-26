import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  SimpleChanges,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { AnalysesApiService, getApiErrorMessage } from '@frontend/api-client';
import { MapOverlay } from '@frontend/map';
import {
  AnalysisDefinition,
  AnalysisEvidenceResult,
  AnalysisEvidenceSource,
  AnalysisParameterDefinition,
  AnalysisRun,
  AnalysisStatus,
  AnalysisType,
  GeoJsonResult,
  LayerGeometryType,
  Site,
} from '@frontend/models';
import { EmptyStateComponent, MetricCardComponent } from '@frontend/ui';
import {
  LucideChartNoAxesCombined,
  LucideCircleAlert,
  LucideCircleCheck,
  LucideClock3,
  LucideDownload,
  LucideEyeOff,
  LucideHistory,
  LucideInfo,
  LucideMapPinned,
  LucideRotateCcw,
  LucideX,
} from '@lucide/angular';
import { finalize, forkJoin } from 'rxjs';

type AnalysisTab = 'overview' | 'hazards' | 'planning' | 'terrain' | 'sources';

const CORE_ANALYSES: AnalysisType[] = ['HazardExposure', 'Zoning', 'Terrain'];

const RESULT_STYLES: Record<
  string,
  { fillColor: string; strokeColor: string; lineColor?: string }
> = {
  'flood-zone': {
    fillColor: '#2563eb',
    strokeColor: '#1d4ed8',
  },
  landslide: {
    fillColor: '#b45309',
    strokeColor: '#92400e',
  },
  'storm-surge': {
    fillColor: '#0891b2',
    strokeColor: '#0e7490',
  },
  'protected-area': {
    fillColor: '#16a34a',
    strokeColor: '#15803d',
  },
  'fault-line-proximity': {
    fillColor: '#dc2626',
    strokeColor: '#b91c1c',
    lineColor: '#dc2626',
  },
  'zoning-classification': {
    fillColor: '#d97706',
    strokeColor: '#b45309',
  },
  'administrative-jurisdiction': {
    fillColor: '#7c3aed',
    strokeColor: '#6d28d9',
  },
  'development-restrictions': {
    fillColor: '#e11d48',
    strokeColor: '#be123c',
  },
  'terrain-profile': {
    fillColor: '#65a30d',
    strokeColor: '#4d7c0f',
  },
};

@Component({
  selector: 'fg-site-analysis',
  standalone: true,
  imports: [
    CommonModule,
    EmptyStateComponent,
    LucideChartNoAxesCombined,
    LucideCircleAlert,
    LucideCircleCheck,
    LucideClock3,
    LucideDownload,
    LucideEyeOff,
    LucideHistory,
    LucideInfo,
    LucideMapPinned,
    MetricCardComponent,
    LucideRotateCcw,
    LucideX,
  ],
  templateUrl: './site-analysis.component.html',
  styleUrl: './site-analysis.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteAnalysisComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) site!: Site;

  readonly resultOverlaysChanged = output<MapOverlay[]>();
  readonly resultLayerRequested = output<AnalysisEvidenceResult>();

  private readonly analysesApi = inject(AnalysesApiService);
  private historyRequestSequence = 0;
  private pollingTimer?: ReturnType<typeof setTimeout>;

  protected readonly tabs: ReadonlyArray<{
    id: AnalysisTab;
    label: string;
  }> = [
    { id: 'overview', label: 'Overview' },
    { id: 'hazards', label: 'Hazards' },
    { id: 'planning', label: 'Planning' },
    { id: 'terrain', label: 'Terrain' },
    { id: 'sources', label: 'Data Sources' },
  ];
  protected readonly activeTab = signal<AnalysisTab>('overview');
  protected readonly catalog = signal<AnalysisDefinition[]>([]);
  protected readonly runs = signal<AnalysisRun[]>([]);
  protected readonly selectedDefinition = signal<AnalysisDefinition | null>(
    null,
  );
  protected readonly selectedRunId = signal<string | null>(null);
  protected readonly selectedRun = computed(() => {
    const selectedId = this.selectedRunId();
    return (
      this.runs().find((run) => run.id === selectedId) ?? this.runs()[0] ?? null
    );
  });
  protected readonly latestCoreRuns = computed(() =>
    CORE_ANALYSES.map((analysisType) =>
      this.runs().find(
        (run) =>
          run.analysisType === analysisType &&
          run.status === 'Completed' &&
          run.result,
      ),
    ).filter((run): run is AnalysisRun => Boolean(run)),
  );
  protected readonly allEvidence = computed(() =>
    this.latestCoreRuns().flatMap((run) => run.result?.results ?? []),
  );
  protected readonly sources = computed(() => {
    const sourcesById = new Map<string, AnalysisEvidenceSource>();

    for (const result of this.allEvidence()) {
      sourcesById.set(result.source.id, result.source);
    }

    return [...sourcesById.values()];
  });
  protected readonly completedCoreCount = computed(
    () => this.latestCoreRuns().length,
  );
  protected readonly coreRunsInProgress = computed(() =>
    this.runs().filter(
      (run) =>
        CORE_ANALYSES.includes(run.analysisType) &&
        (run.status === 'Pending' || run.status === 'Running'),
    ),
  );
  protected readonly notableEvidence = computed(() =>
    this.allEvidence().filter(
      (result) => result.severity === 'High' || result.severity === 'Moderate',
    ),
  );
  protected readonly terrainMetrics = computed(
    () => this.latestRun('Terrain', 'Completed')?.result?.metrics ?? {},
  );
  protected readonly parameterValues = signal<Record<string, unknown>>({});
  protected readonly visibleResultIds = signal<ReadonlySet<string>>(new Set());
  protected readonly selectedSourceId = signal<string | null>(null);
  protected readonly catalogLoading = signal(true);
  protected readonly historyLoading = signal(true);
  protected readonly submittingType = signal<AnalysisType | null>(null);
  protected readonly assessmentSubmitting = signal(false);
  protected readonly cancellingId = signal<string | null>(null);
  protected readonly catalogError = signal<string | null>(null);
  protected readonly historyError = signal<string | null>(null);
  protected readonly requestError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadCatalog();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['site'] && this.site?.id) {
      this.runs.set([]);
      this.activeTab.set('overview');
      this.selectedDefinition.set(null);
      this.selectedRunId.set(null);
      this.parameterValues.set({});
      this.visibleResultIds.set(new Set());
      this.selectedSourceId.set(null);
      this.requestError.set(null);
      this.emitResultOverlays();
      this.loadHistory(this.site.id, true);
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
    this.resultOverlaysChanged.emit([]);
  }

  protected selectTab(tab: AnalysisTab): void {
    this.activeTab.set(tab);
  }

  protected latestRun(
    analysisType: AnalysisType,
    status?: AnalysisStatus,
  ): AnalysisRun | undefined {
    return this.runs().find(
      (run) =>
        run.analysisType === analysisType &&
        (status === undefined || run.status === status),
    );
  }

  protected evidenceFor(category: string): AnalysisEvidenceResult[] {
    return this.allEvidence().filter((result) => result.category === category);
  }

  protected sourceResults(sourceId: string): AnalysisEvidenceResult[] {
    return this.allEvidence().filter((result) => result.source.id === sourceId);
  }

  protected runCoreAssessment(): void {
    if (this.assessmentSubmitting() || this.coreRunsInProgress().length > 0) {
      return;
    }

    const definitions = CORE_ANALYSES.map((analysisType) =>
      this.catalog().find(
        (definition) => definition.analysisType === analysisType,
      ),
    ).filter((definition): definition is AnalysisDefinition =>
      Boolean(definition),
    );

    if (definitions.length !== CORE_ANALYSES.length) {
      this.requestError.set(
        'The core intelligence catalog is incomplete. Reload the catalog and try again.',
      );
      return;
    }

    this.assessmentSubmitting.set(true);
    this.requestError.set(null);

    forkJoin(
      definitions.map((definition) =>
        this.analysesApi.createAnalysis(this.site.id, {
          analysisType: definition.analysisType,
          inputParameters: Object.fromEntries(
            definition.parameters.map((parameter) => [
              parameter.name,
              parameter.defaultValue,
            ]),
          ),
        }),
      ),
    )
      .pipe(finalize(() => this.assessmentSubmitting.set(false)))
      .subscribe({
        next: (createdRuns) => {
          const createdIds = new Set(createdRuns.map((run) => run.id));
          this.runs.update((runs) => [
            ...createdRuns,
            ...runs.filter((run) => !createdIds.has(run.id)),
          ]);
          this.selectedRunId.set(createdRuns[0]?.id ?? null);
          this.schedulePolling();
        },
        error: (error: unknown) => {
          this.requestError.set(
            getApiErrorMessage(
              error,
              'The core assessment could not be queued.',
            ),
          );
          this.loadHistory(this.site.id, false);
        },
      });
  }

  protected openRequestForm(definition: AnalysisDefinition): void {
    this.selectedDefinition.set(definition);
    this.requestError.set(null);
    this.parameterValues.set(
      Object.fromEntries(
        definition.parameters.map((parameter) => [
          parameter.name,
          parameter.defaultValue,
        ]),
      ),
    );
  }

  protected closeRequestForm(): void {
    if (this.submittingType()) {
      return;
    }

    this.selectedDefinition.set(null);
    this.requestError.set(null);
  }

  protected parameterValue(parameter: AnalysisParameterDefinition): unknown {
    return this.parameterValues()[parameter.name] ?? parameter.defaultValue;
  }

  protected updateParameter(
    parameter: AnalysisParameterDefinition,
    event: Event,
  ): void {
    const input = event.target as HTMLInputElement;
    let value: unknown = input.value;

    if (parameter.type === 'number') {
      value = input.valueAsNumber;
    } else if (parameter.type === 'boolean') {
      value = input.checked;
    }

    this.parameterValues.update((values) => ({
      ...values,
      [parameter.name]: value,
    }));
  }

  protected submitAnalysis(): void {
    const definition = this.selectedDefinition();

    if (!definition || this.submittingType()) {
      return;
    }

    this.requestAnalysis(definition.analysisType, this.parameterValues(), true);
  }

  protected selectRun(run: AnalysisRun): void {
    this.selectedRunId.set(run.id);
    this.requestError.set(null);
  }

  protected cancelRun(run: AnalysisRun): void {
    if (!this.canCancel(run) || this.cancellingId()) {
      return;
    }

    this.cancellingId.set(run.id);
    this.requestError.set(null);

    this.analysesApi
      .cancelAnalysis(run.id)
      .pipe(finalize(() => this.cancellingId.set(null)))
      .subscribe({
        next: () => {
          this.runs.update((runs) =>
            runs.map((candidate) =>
              candidate.id === run.id
                ? {
                    ...candidate,
                    status: 'Cancelled',
                    completedAtUtc: new Date().toISOString(),
                  }
                : candidate,
            ),
          );
          this.schedulePolling();
        },
        error: (error: unknown) => {
          this.requestError.set(
            getApiErrorMessage(error, 'The analysis could not be cancelled.'),
          );
          this.loadHistory(this.site.id, false);
        },
      });
  }

  protected retryRun(run: AnalysisRun): void {
    if (this.submittingType()) {
      return;
    }

    this.requestAnalysis(run.analysisType, run.inputParameters, false);
  }

  protected canCancel(run: AnalysisRun): boolean {
    return run.status === 'Pending' || run.status === 'Running';
  }

  protected statusMessage(run: AnalysisRun): string {
    switch (run.status) {
      case 'Pending':
        return 'Queued and waiting for an available analysis worker.';
      case 'Running':
        return 'Spatial datasets are loaded and operations are in progress.';
      case 'Completed':
        return 'Analysis completed successfully.';
      case 'Failed':
        return 'Analysis could not be completed.';
      case 'Cancelled':
        return 'Analysis was cancelled before completion.';
    }
  }

  protected resultMetrics(run: AnalysisRun): [string, unknown][] {
    return Object.entries(run.result?.metrics ?? {});
  }

  protected metricLabel(value: string): string {
    const spaced = value
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2');
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
  }

  protected metricValue(value: unknown): string {
    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }

    if (typeof value === 'number') {
      return value.toLocaleString(undefined, {
        maximumFractionDigits: 2,
      });
    }

    if (value === null || value === undefined || value === '') {
      return 'Not available';
    }

    return String(value);
  }

  protected metric(values: Record<string, unknown>, key: string): string {
    return this.metricValue(values[key]);
  }

  protected definitionName(analysisType: AnalysisType): string {
    return (
      this.catalog().find(
        (definition) => definition.analysisType === analysisType,
      )?.name ?? analysisType
    );
  }

  protected severityClass(severity: string): string {
    return `severity--${severity.toLocaleLowerCase()}`;
  }

  protected affectedArea(result: AnalysisEvidenceResult): string {
    const area = result.intersectionAreaSquareMetres;

    if (area >= 1_000_000) {
      return `${(area / 1_000_000).toLocaleString(undefined, {
        maximumFractionDigits: 2,
      })} km²`;
    }

    if (area >= 10_000) {
      return `${(area / 10_000).toLocaleString(undefined, {
        maximumFractionDigits: 2,
      })} ha`;
    }

    return `${area.toLocaleString(undefined, {
      maximumFractionDigits: 0,
    })} m²`;
  }

  protected affectedPercent(result: AnalysisEvidenceResult): string {
    return `${result.sitePercent.toLocaleString(undefined, {
      maximumFractionDigits: 2,
    })}%`;
  }

  protected intersections(
    result: AnalysisEvidenceResult,
  ): Array<Record<string, unknown>> {
    const value = result.details['intersections'];
    return Array.isArray(value)
      ? (value as Array<Record<string, unknown>>)
      : [];
  }

  protected intersectionValue(
    value: Record<string, unknown>,
    key: string,
  ): string {
    return this.metricValue(value[key]);
  }

  protected layerVisible(result: AnalysisEvidenceResult): boolean {
    return this.visibleResultIds().has(result.id);
  }

  protected toggleEvidenceLayer(result: AnalysisEvidenceResult): void {
    if (!result.resultGeometry) {
      return;
    }

    this.visibleResultIds.update((visibleIds) => {
      const nextIds = new Set(visibleIds);
      if (nextIds.has(result.id)) {
        nextIds.delete(result.id);
      } else {
        nextIds.add(result.id);
      }
      return nextIds;
    });
    this.emitResultOverlays();

    if (this.layerVisible(result)) {
      this.resultLayerRequested.emit(result);
    }
  }

  protected showSource(sourceId: string): void {
    this.selectedSourceId.set(sourceId);
    this.activeTab.set('sources');
    setTimeout(() =>
      document
        .getElementById(`analysis-source-${sourceId}`)
        ?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }),
    );
  }

  protected downloadResultGeometry(run: AnalysisRun): void {
    const geometry = run.result?.resultGeometry;
    if (!geometry) {
      return;
    }

    const blob = new Blob(
      [JSON.stringify(this.asFeatureCollection(geometry), null, 2)],
      { type: 'application/geo+json' },
    );
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${run.analysisType
      .replace(/([a-z])([A-Z])/g, '$1-$2')
      .toLocaleLowerCase()}-${run.id.slice(0, 8)}.geojson`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected retryCatalog(): void {
    this.loadCatalog();
  }

  protected retryHistory(): void {
    this.loadHistory(this.site.id, true);
  }

  protected statusClass(status: AnalysisStatus): string {
    return `analysis-status--${status.toLocaleLowerCase()}`;
  }

  private requestAnalysis(
    analysisType: AnalysisType,
    inputParameters: Record<string, unknown>,
    closeFormOnSuccess: boolean,
  ): void {
    this.submittingType.set(analysisType);
    this.requestError.set(null);

    this.analysesApi
      .createAnalysis(this.site.id, {
        analysisType,
        inputParameters,
      })
      .pipe(finalize(() => this.submittingType.set(null)))
      .subscribe({
        next: (run) => {
          this.runs.update((runs) => [
            run,
            ...runs.filter((candidate) => candidate.id !== run.id),
          ]);
          this.selectedRunId.set(run.id);
          if (closeFormOnSuccess) {
            this.selectedDefinition.set(null);
          }
          this.schedulePolling();
        },
        error: (error: unknown) => {
          this.requestError.set(
            getApiErrorMessage(
              error,
              'The analysis request could not be created.',
            ),
          );
        },
      });
  }

  private loadCatalog(): void {
    this.catalogLoading.set(true);
    this.catalogError.set(null);

    this.analysesApi
      .getCatalog()
      .pipe(finalize(() => this.catalogLoading.set(false)))
      .subscribe({
        next: (catalog) => this.catalog.set(catalog),
        error: (error: unknown) => {
          this.catalogError.set(
            getApiErrorMessage(
              error,
              'The analysis catalog could not be loaded.',
            ),
          );
        },
      });
  }

  private loadHistory(siteId: string, showLoading: boolean): void {
    const requestSequence = ++this.historyRequestSequence;
    this.stopPolling();

    if (showLoading) {
      this.historyLoading.set(true);
    }
    this.historyError.set(null);

    this.analysesApi
      .getSiteAnalyses(siteId)
      .pipe(
        finalize(() => {
          if (requestSequence === this.historyRequestSequence) {
            this.historyLoading.set(false);
          }
        }),
      )
      .subscribe({
        next: (runs) => {
          if (
            requestSequence !== this.historyRequestSequence ||
            siteId !== this.site.id
          ) {
            return;
          }

          this.runs.set(runs);
          const selectedId = this.selectedRunId();
          if (selectedId && !runs.some((run) => run.id === selectedId)) {
            this.selectedRunId.set(runs[0]?.id ?? null);
          }
          this.emitResultOverlays();
          this.schedulePolling();
        },
        error: (error: unknown) => {
          if (
            requestSequence !== this.historyRequestSequence ||
            siteId !== this.site.id
          ) {
            return;
          }

          this.historyError.set(
            getApiErrorMessage(error, 'Previous analyses could not be loaded.'),
          );
          this.schedulePolling();
        },
      });
  }

  private schedulePolling(): void {
    this.stopPolling();

    if (
      !this.runs().some(
        (run) => run.status === 'Pending' || run.status === 'Running',
      )
    ) {
      return;
    }

    const siteId = this.site.id;
    this.pollingTimer = setTimeout(() => this.loadHistory(siteId, false), 2000);
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearTimeout(this.pollingTimer);
      this.pollingTimer = undefined;
    }
  }

  private emitResultOverlays(): void {
    if (!this.site?.id) {
      this.resultOverlaysChanged.emit([]);
      return;
    }

    this.resultOverlaysChanged.emit(
      this.allEvidence()
        .filter((result) => result.resultGeometry)
        .map((result, index) => this.createResultOverlay(result, index)),
    );
  }

  private createResultOverlay(
    result: AnalysisEvidenceResult,
    index: number,
  ): MapOverlay {
    const source = result.source;
    const style = RESULT_STYLES[result.id] ?? RESULT_STYLES['terrain-profile'];
    const geometryType = this.resultGeometryType(result.resultGeometry);

    return {
      id: `analysis-${this.site.id}-${result.id}`,
      name: result.name,
      description: result.summary,
      category:
        result.category === 'Terrain'
          ? 'Terrain'
          : result.category === 'Planning'
            ? 'Planning'
            : result.id === 'protected-area'
              ? 'Environment'
              : 'Hazards',
      geographicCoverage: `${this.site.name} result intersection`,
      coordinateSystem: source.coordinateSystem,
      geometryType,
      featureNameProperty: 'name',
      style: {
        fillColor: style.fillColor,
        fillOpacity: 0.4,
        strokeColor: style.strokeColor,
        strokeWidth: 2,
        lineColor: style.lineColor ?? style.strokeColor,
        lineWidth: result.id === 'fault-line-proximity' ? 3 : 2,
      },
      dataSource: {
        id: source.id,
        name: source.dataset,
        organization: source.organization,
        licenseName: source.license,
        licenseUrl:
          source.license === 'CC0 1.0'
            ? 'https://creativecommons.org/publicdomain/zero/1.0/'
            : null,
        attribution: `${source.organization} — ${source.dataset}`,
        sourceUrl: source.sourceUrl,
      },
      lastUpdatedAtUtc: `${source.publishedDate}T00:00:00Z`,
      deliveryMethod: 'GeoJson',
      dataUrl: '',
      data: result.resultGeometry ?? undefined,
      sourceLayer: null,
      minimumZoom: null,
      maximumZoom: null,
      legend: [
        {
          id: `${result.id}-legend`,
          label: `${result.classification} · ${result.severity}`,
          fillColor: style.fillColor,
          strokeColor: style.strokeColor,
          symbol: null,
          sortOrder: 0,
        },
      ],
      visible: this.visibleResultIds().has(result.id),
      opacity: 0.85,
      sortOrder: 1000 + index,
      filter: null,
    };
  }

  private resultGeometryType(
    resultGeometry: GeoJsonResult | null,
  ): LayerGeometryType {
    if (!resultGeometry) {
      return 'Polygon';
    }

    const feature =
      resultGeometry.type === 'Feature'
        ? resultGeometry
        : (resultGeometry.features[0] as
            | {
                geometry?: { type?: string };
              }
            | undefined);
    const geometryType =
      resultGeometry.type === 'Feature'
        ? String(resultGeometry.geometry['type'] ?? '')
        : String(feature?.geometry?.type ?? '');

    if (geometryType.includes('LineString')) {
      return 'LineString';
    }
    if (geometryType.includes('Point')) {
      return 'Point';
    }
    return 'Polygon';
  }

  private asFeatureCollection(geometry: GeoJsonResult): GeoJsonResult {
    return geometry.type === 'Feature'
      ? {
          type: 'FeatureCollection',
          features: [geometry],
        }
      : geometry;
  }
}
