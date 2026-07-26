import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
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
  ReportsApiService,
  getApiErrorMessage,
} from '@frontend/api-client';
import { MapComponent, MapFeature } from '@frontend/map';
import {
  CreateReportRequest,
  GeneratedReport,
  ReportFormat,
  ReportPreview,
  ReportPreviewSection,
  ReportSectionKey,
  ReportSectionSelection,
} from '@frontend/models';
import { AlertComponent, PageStateComponent } from '@frontend/ui';
import {
  LucideArrowDown,
  LucideArrowLeft,
  LucideArrowUp,
  LucideCheck,
  LucideClock3,
  LucideDatabase,
  LucideDownload,
  LucideEye,
  LucideFileText,
  LucideMap,
  LucidePalette,
} from '@lucide/angular';
import { finalize } from 'rxjs';

type ReportBuilderSource = 'site' | 'comparison';

interface SectionOption extends ReportSectionSelection {
  label: string;
  description: string;
}

interface ExportOption {
  format: ReportFormat;
  label: string;
  description: string;
}

const SECTION_CATALOG: ReadonlyArray<
  Pick<SectionOption, 'key' | 'label' | 'description'>
> = [
  {
    key: 'Cover',
    label: 'Cover',
    description: 'Report title, project, branding, and generation date.',
  },
  {
    key: 'ExecutiveSummary',
    label: 'Executive summary',
    description: 'Decision snapshot and highest-priority observations.',
  },
  {
    key: 'SiteLocation',
    label: 'Site location',
    description: 'Consistently framed site boundary map.',
  },
  {
    key: 'GeometrySummary',
    label: 'Geometry summary',
    description: 'Area, perimeter, centroid, CRS, and vertex count.',
  },
  {
    key: 'HazardFindings',
    label: 'Hazard findings',
    description: 'Exposure, intersection, and proximity evidence.',
  },
  {
    key: 'PlanningFindings',
    label: 'Planning findings',
    description: 'Zoning, jurisdiction, and development restrictions.',
  },
  {
    key: 'SuitabilityScore',
    label: 'Suitability score',
    description: 'Overall score with criterion-level traceability.',
  },
  {
    key: 'SiteComparison',
    label: 'Site comparison',
    description: 'Ranked candidates under one saved scoring model.',
  },
  {
    key: 'DataSources',
    label: 'Data sources',
    description: 'Attribution, versions, licenses, and source links.',
  },
  {
    key: 'Disclaimer',
    label: 'Disclaimer',
    description: 'Limitations and appropriate decision use.',
  },
];

@Component({
  selector: 'fg-report-builder',
  standalone: true,
  imports: [
    AlertComponent,
    CommonModule,
    LucideArrowDown,
    LucideArrowLeft,
    LucideArrowUp,
    LucideCheck,
    LucideClock3,
    LucideDatabase,
    LucideDownload,
    LucideEye,
    LucideFileText,
    LucideMap,
    LucidePalette,
    MapComponent,
    PageStateComponent,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './report-builder.component.html',
  styleUrl: './report-builder.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportBuilderComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reportsApi = inject(ReportsApiService);

  protected readonly sourceType = this.route.snapshot.data[
    'sourceType'
  ] as ReportBuilderSource;
  protected readonly sourceId =
    this.route.snapshot.paramMap.get(
      this.sourceType === 'site' ? 'siteId' : 'comparisonId',
    ) ?? '';

  protected readonly loading = signal(true);
  protected readonly previewLoading = signal(false);
  protected readonly generating = signal(false);
  protected readonly exportingFormat = signal<ReportFormat | null>(null);
  protected readonly downloadingId = signal<string | null>(null);
  protected readonly previewDirty = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly preview = signal<ReportPreview | null>(null);
  protected readonly reports = signal<GeneratedReport[]>([]);
  protected readonly generatedReport = signal<GeneratedReport | null>(null);
  protected readonly sections = signal<SectionOption[]>(
    SECTION_CATALOG.map((section, index) => ({
      ...section,
      included:
        section.key !==
        (this.sourceType === 'site' ? 'SiteComparison' : 'SuitabilityScore'),
      sortOrder: index,
    })),
  );

  protected readonly reportForm = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    organizationName: new FormControl('FormaGeo', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    preparedBy: new FormControl('FormaGeo analysis team', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    accentColor: new FormControl('#0F766E', {
      nonNullable: true,
      validators: [Validators.pattern(/^#[0-9A-Fa-f]{6}$/)],
    }),
    footerText: new FormControl(
      'Prepared with FormaGeo spatial decision support',
      {
        nonNullable: true,
        validators: [Validators.maxLength(180)],
      },
    ),
  });

  protected readonly includedCount = computed(
    () => this.sections().filter((section) => section.included).length,
  );
  protected readonly visiblePreviewSections = computed(() =>
    (this.preview()?.sections ?? [])
      .filter((section) => section.included)
      .sort((left, right) => left.sortOrder - right.sortOrder),
  );
  protected readonly mapFeatures = computed<MapFeature[]>(() =>
    (this.preview()?.mapSites ?? []).map((site) => ({
      id: site.siteId,
      geometry: {
        type: 'Polygon',
        coordinates: [site.boundary],
      },
      properties: {
        name: site.siteName,
        status: 'Active',
        comparisonSelected: this.sourceType === 'comparison',
      },
    })),
  );
  protected readonly exportOptions = computed<ExportOption[]>(() =>
    this.sourceType === 'site'
      ? [
          {
            format: 'MetricsCsv',
            label: 'Metrics CSV',
            description: 'Measurements and suitability values.',
          },
          {
            format: 'GeometryGeoJson',
            label: 'Site GeoJSON',
            description: 'WGS 84 boundary with project metadata.',
          },
          {
            format: 'AnalysisGeoJson',
            label: 'Analysis GeoJSON',
            description: 'Result geometries and evidence attributes.',
          },
        ]
      : [
          {
            format: 'ComparisonCsv',
            label: 'Comparison CSV',
            description: 'Ranks, scores, ratings, and metric values.',
          },
        ],
  );

  ngOnInit(): void {
    if (!this.sourceId) {
      this.loading.set(false);
      this.errorMessage.set('No report source ID was provided.');
      return;
    }

    this.loadPreview(true);
    this.loadHistory();
  }

  protected toggleSection(
    key: ReportSectionKey,
    included: boolean,
  ): void {
    if (!included && this.includedCount() === 1) return;

    this.sections.update((sections) =>
      sections.map((section) =>
        section.key === key ? { ...section, included } : section,
      ),
    );
    this.markPreviewDirty();
  }

  protected moveSection(
    key: ReportSectionKey,
    direction: -1 | 1,
  ): void {
    this.sections.update((sections) => {
      const next = [...sections];
      const index = next.findIndex((section) => section.key === key);
      const target = index + direction;
      if (index < 0 || target < 0 || target >= next.length) return sections;

      [next[index], next[target]] = [next[target], next[index]];
      return next.map((section, sortOrder) => ({
        ...section,
        sortOrder,
      }));
    });
    this.markPreviewDirty();
  }

  protected markPreviewDirty(): void {
    if (this.preview()) {
      this.previewDirty.set(true);
    }
    this.generatedReport.set(null);
  }

  protected refreshPreview(): void {
    if (this.reportForm.invalid) {
      this.reportForm.markAllAsTouched();
      return;
    }
    this.loadPreview(false);
  }

  protected generatePdf(): void {
    if (
      this.reportForm.invalid ||
      this.generating() ||
      this.includedCount() === 0
    ) {
      this.reportForm.markAllAsTouched();
      return;
    }

    this.generating.set(true);
    this.errorMessage.set(null);
    this.generatedReport.set(null);
    const request = this.buildRequest('Pdf');
    const operation =
      this.sourceType === 'site'
        ? this.reportsApi.createSiteReport(this.sourceId, request)
        : this.reportsApi.createComparisonReport(this.sourceId, request);

    operation.pipe(finalize(() => this.generating.set(false))).subscribe({
      next: (report) => {
        this.generatedReport.set(report);
        this.previewDirty.set(false);
        this.reports.update((reports) => [
          report,
          ...reports.filter((item) => item.id !== report.id),
        ]);
      },
      error: (error: unknown) => {
        this.errorMessage.set(
          getApiErrorMessage(error, 'The PDF report could not be generated.'),
        );
      },
    });
  }

  protected generateExport(format: ReportFormat): void {
    if (this.exportingFormat()) return;

    this.exportingFormat.set(format);
    this.errorMessage.set(null);
    const request = this.buildRequest(format);
    const operation =
      this.sourceType === 'site'
        ? this.reportsApi.createSiteReport(this.sourceId, request)
        : this.reportsApi.createComparisonReport(this.sourceId, request);

    operation
      .pipe(finalize(() => this.exportingFormat.set(null)))
      .subscribe({
        next: (report) => {
          this.reports.update((reports) => [report, ...reports]);
          this.download(report);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The data export could not be generated.'),
          );
        },
      });
  }

  protected download(report: GeneratedReport): void {
    if (this.downloadingId()) return;

    this.downloadingId.set(report.id);
    this.errorMessage.set(null);
    this.reportsApi
      .downloadReport(report.id)
      .pipe(finalize(() => this.downloadingId.set(null)))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = report.fileName;
          link.click();
          URL.revokeObjectURL(url);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'The generated file could not be downloaded.',
            ),
          );
        },
      });
  }

  protected previewSection(
    key: ReportSectionKey,
  ): ReportPreviewSection | undefined {
    return this.visiblePreviewSections().find((section) => section.key === key);
  }

  protected findings(category: string) {
    return (
      this.preview()?.findings.filter(
        (finding) => finding.category === category,
      ) ?? []
    );
  }

  protected severityClass(severity: string): string {
    return `finding--${severity.toLowerCase().replace(/\s+/g, '-')}`;
  }

  protected formatLabel(format: ReportFormat): string {
    const labels: Record<ReportFormat, string> = {
      Pdf: 'PDF report',
      MetricsCsv: 'Metrics CSV',
      GeometryGeoJson: 'Site GeoJSON',
      AnalysisGeoJson: 'Analysis GeoJSON',
      ComparisonCsv: 'Comparison CSV',
      ProjectArchive: 'Project archive',
    };
    return labels[format];
  }

  protected formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  private loadPreview(initial: boolean): void {
    if (initial) {
      this.loading.set(true);
    } else {
      this.previewLoading.set(true);
    }
    this.errorMessage.set(null);
    const request = this.buildRequest('Pdf');
    const operation =
      this.sourceType === 'site'
        ? this.reportsApi.previewSiteReport(this.sourceId, request)
        : this.reportsApi.previewComparisonReport(this.sourceId, request);

    operation
      .pipe(
        finalize(() => {
          this.loading.set(false);
          this.previewLoading.set(false);
        }),
      )
      .subscribe({
        next: (preview) => {
          this.preview.set(preview);
          if (initial && !this.reportForm.controls.title.value) {
            this.reportForm.controls.title.setValue(preview.title);
          }
          this.previewDirty.set(false);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The report preview could not be loaded.'),
          );
        },
      });
  }

  private loadHistory(): void {
    const operation =
      this.sourceType === 'site'
        ? this.reportsApi.getSiteReports(this.sourceId)
        : this.reportsApi.getComparisonReports(this.sourceId);

    operation.subscribe({
      next: (reports) => this.reports.set(reports),
      error: (error: unknown) => {
        this.errorMessage.set(
          getApiErrorMessage(error, 'Report history could not be loaded.'),
        );
      },
    });
  }

  private buildRequest(format: ReportFormat): CreateReportRequest {
    const values = this.reportForm.getRawValue();
    return {
      title: values.title,
      format,
      sections: this.sections().map(
        ({ key, included, sortOrder }): ReportSectionSelection => ({
          key,
          included,
          sortOrder,
        }),
      ),
      branding: {
        organizationName: values.organizationName,
        preparedBy: values.preparedBy,
        accentColor: values.accentColor,
        footerText: values.footerText,
      },
    };
  }
}
