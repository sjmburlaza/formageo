import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  signal,
  SimpleChanges,
  computed,
  inject,
} from '@angular/core';
import {
  AnalysesApiService,
  getApiErrorMessage,
} from '@frontend/api-client';
import {
  AnalysisDefinition,
  AnalysisParameterDefinition,
  AnalysisRun,
  AnalysisStatus,
  AnalysisType,
  GeoJsonResult,
  Site,
} from '@frontend/models';
import {
  LucideChartNoAxesCombined,
  LucideCircleAlert,
  LucideCircleCheck,
  LucideClock3,
  LucideDownload,
  LucideHistory,
  LucideRotateCcw,
  LucideX,
} from '@lucide/angular';
import { finalize } from 'rxjs';

@Component({
  selector: 'fg-site-analysis',
  standalone: true,
  imports: [
    CommonModule,
    LucideChartNoAxesCombined,
    LucideCircleAlert,
    LucideCircleCheck,
    LucideClock3,
    LucideDownload,
    LucideHistory,
    LucideRotateCcw,
    LucideX,
  ],
  templateUrl: './site-analysis.component.html',
  styleUrl: './site-analysis.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteAnalysisComponent
  implements OnInit, OnChanges, OnDestroy
{
  @Input({ required: true }) site!: Site;

  private readonly analysesApi = inject(AnalysesApiService);
  private historyRequestSequence = 0;
  private pollingTimer?: ReturnType<typeof setTimeout>;

  protected readonly catalog = signal<AnalysisDefinition[]>([]);
  protected readonly runs = signal<AnalysisRun[]>([]);
  protected readonly selectedDefinition = signal<AnalysisDefinition | null>(
    null,
  );
  protected readonly selectedRunId = signal<string | null>(null);
  protected readonly selectedRun = computed(() => {
    const selectedId = this.selectedRunId();
    return (
      this.runs().find((run) => run.id === selectedId) ??
      this.runs()[0] ??
      null
    );
  });
  protected readonly parameterValues = signal<Record<string, unknown>>({});
  protected readonly catalogLoading = signal(true);
  protected readonly historyLoading = signal(true);
  protected readonly submittingType = signal<AnalysisType | null>(null);
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
      this.selectedDefinition.set(null);
      this.selectedRunId.set(null);
      this.parameterValues.set({});
      this.requestError.set(null);
      this.loadHistory(this.site.id, true);
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  protected latestRun(
    analysisType: AnalysisType,
  ): AnalysisRun | undefined {
    return this.runs().find(
      (run) => run.analysisType === analysisType,
    );
  }

  protected openRequestForm(
    definition: AnalysisDefinition,
  ): void {
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

  protected parameterValue(
    parameter: AnalysisParameterDefinition,
  ): unknown {
    return (
      this.parameterValues()[parameter.name] ??
      parameter.defaultValue
    );
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

    this.requestAnalysis(
      definition.analysisType,
      this.parameterValues(),
      true,
    );
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
            getApiErrorMessage(
              error,
              'The analysis could not be cancelled.',
            ),
          );
          this.loadHistory(this.site.id, false);
        },
      });
  }

  protected retryRun(run: AnalysisRun): void {
    if (this.submittingType()) {
      return;
    }

    this.requestAnalysis(
      run.analysisType,
      run.inputParameters,
      false,
    );
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

  protected resultMetrics(
    run: AnalysisRun,
  ): [string, unknown][] {
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

  protected definitionName(analysisType: AnalysisType): string {
    return (
      this.catalog().find(
        (definition) =>
          definition.analysisType === analysisType,
      )?.name ?? analysisType
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

  private loadHistory(
    siteId: string,
    showLoading: boolean,
  ): void {
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
          if (
            selectedId &&
            !runs.some((run) => run.id === selectedId)
          ) {
            this.selectedRunId.set(runs[0]?.id ?? null);
          }
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
            getApiErrorMessage(
              error,
              'Previous analyses could not be loaded.',
            ),
          );
          this.schedulePolling();
        },
      });
  }

  private schedulePolling(): void {
    this.stopPolling();

    if (
      !this.runs().some(
        (run) =>
          run.status === 'Pending' || run.status === 'Running',
      )
    ) {
      return;
    }

    const siteId = this.site.id;
    this.pollingTimer = setTimeout(
      () => this.loadHistory(siteId, false),
      2000,
    );
  }

  private stopPolling(): void {
    if (this.pollingTimer) {
      clearTimeout(this.pollingTimer);
      this.pollingTimer = undefined;
    }
  }

  private asFeatureCollection(
    geometry: GeoJsonResult,
  ): GeoJsonResult {
    return geometry.type === 'Feature'
      ? {
          type: 'FeatureCollection',
          features: [geometry],
        }
      : geometry;
  }
}
