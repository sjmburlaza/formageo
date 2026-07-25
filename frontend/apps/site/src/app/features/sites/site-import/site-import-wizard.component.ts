import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import {
  getApiErrorMessage,
  ProjectsApiService,
  SitesApiService,
} from '@frontend/api-client';
import { MapComponent, MapFeature } from '@frontend/map';
import {
  Project,
  SiteImportMode,
  SiteImportResult,
} from '@frontend/models';
import {
  LucideArrowLeft,
  LucideArrowRight,
  LucideCheck,
  LucideCircleAlert,
  LucideCircleCheck,
  LucideClipboardPaste,
  LucideFileJson,
  LucideFileUp,
  LucideMapPinned,
  LucideUpload,
  LucideX,
} from '@lucide/angular';
import { finalize } from 'rxjs';
import {
  GeoJsonImportPreview,
  GeoJsonPreviewError,
  parseGeoJsonImport,
} from './geojson-import.parser';
import { SiteImportCompletedEvent } from './site-import.models';

type WizardStep = 1 | 2 | 3 | 4;
type SourceMode = 'file' | 'paste';

@Component({
  selector: 'fg-site-import-wizard',
  standalone: true,
  imports: [
    CommonModule,
    LucideArrowLeft,
    LucideArrowRight,
    LucideCheck,
    LucideCircleAlert,
    LucideCircleCheck,
    LucideClipboardPaste,
    LucideFileJson,
    LucideFileUp,
    LucideMapPinned,
    LucideUpload,
    LucideX,
    MapComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './site-import-wizard.component.html',
  styleUrl: './site-import-wizard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteImportWizardComponent implements OnInit {
  private static readonly maximumFileSize = 1_048_576;

  private readonly projectsApi = inject(ProjectsApiService);
  private readonly sitesApi = inject(SitesApiService);
  private readonly router = inject(Router);
  private selectedFile: File | null = null;

  @Input({ required: true })
  currentProjectId = '';

  @Output()
  readonly closed = new EventEmitter<void>();

  @Output()
  readonly completed = new EventEmitter<SiteImportCompletedEvent>();

  protected readonly step = signal<WizardStep>(1);
  protected readonly sourceMode = signal<SourceMode>('file');
  protected readonly dragging = signal(false);
  protected readonly parsing = signal(false);
  protected readonly submitting = signal(false);
  protected readonly projectsLoading = signal(true);
  protected readonly projects = signal<Project[]>([]);
  protected readonly preview = signal<GeoJsonImportPreview | null>(null);
  protected readonly selectedFeatureIndexes = signal<ReadonlySet<number>>(
    new Set(),
  );
  protected readonly result = signal<SiteImportResult | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly pasteContent = signal('');

  protected readonly validFeatures = computed(
    () =>
      this.preview()?.features.filter(
        (feature) =>
          feature.issues.length === 0 && feature.polygons.length > 0,
      ) ?? [],
  );
  protected readonly selectedCount = computed(
    () => this.selectedFeatureIndexes().size,
  );
  protected readonly previewMapFeatures = computed<MapFeature[]>(() =>
    this.validFeatures().flatMap((feature) =>
      feature.polygons.map((polygon, polygonIndex) => ({
        id: `${feature.index}-${polygonIndex}`,
        geometry: polygon,
        visible: this.selectedFeatureIndexes().has(feature.index),
        properties: {
          name: feature.name,
          status: 'Active',
        },
      })),
    ),
  );
  protected readonly selectedPolygonCount = computed(() =>
    this.validFeatures()
      .filter((feature) =>
        this.selectedFeatureIndexes().has(feature.index),
      )
      .reduce(
        (total, feature) => total + feature.polygons.length,
        0,
      ),
  );

  protected readonly configureForm = new FormGroup({
    namePattern: new FormControl('{feature}', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    targetProjectId: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    mode: new FormControl<SiteImportMode>('Separate', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  ngOnInit(): void {
    this.configureForm.controls.targetProjectId.setValue(
      this.currentProjectId,
    );
    this.projectsApi
      .getProjects()
      .pipe(finalize(() => this.projectsLoading.set(false)))
      .subscribe({
        next: (projects) => this.projects.set(projects),
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'Projects could not be loaded. Try opening the importer again.',
            ),
          );
        },
      });
  }

  protected setSourceMode(mode: SourceMode): void {
    this.sourceMode.set(mode);
    this.errorMessage.set(null);
  }

  protected updatePasteContent(event: Event): void {
    this.pasteContent.set(
      (event.target as HTMLTextAreaElement).value,
    );
  }

  protected handleDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected handleDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
  }

  protected handleDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files.item(0);

    if (file) {
      void this.readFile(file);
    }
  }

  protected handleFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);

    if (file) {
      void this.readFile(file);
    }

    input.value = '';
  }

  protected parsePastedGeoJson(): void {
    const content = this.pasteContent().trim();

    if (!content) {
      this.errorMessage.set('Paste GeoJSON content before continuing.');
      return;
    }

    const file = new File(
      [content],
      'pasted-boundary.geojson',
      { type: 'application/geo+json' },
    );

    this.parseSource(file, content);
  }

  protected toggleFeature(
    featureIndex: number,
    event: Event,
  ): void {
    const checked = (event.target as HTMLInputElement).checked;

    this.selectedFeatureIndexes.update((current) => {
      const next = new Set(current);

      if (checked) {
        next.add(featureIndex);
      } else {
        next.delete(featureIndex);
      }

      return next;
    });
  }

  protected selectAllFeatures(): void {
    this.selectedFeatureIndexes.set(
      new Set(this.validFeatures().map((feature) => feature.index)),
    );
  }

  protected goToConfigure(): void {
    if (this.selectedCount() === 0) {
      this.errorMessage.set(
        'Select at least one valid feature to import.',
      );
      return;
    }

    this.errorMessage.set(null);
    this.step.set(3);
  }

  protected goBack(): void {
    this.errorMessage.set(null);

    if (this.step() === 3) {
      this.step.set(2);
    } else if (this.step() === 2) {
      this.preview.set(null);
      this.selectedFeatureIndexes.set(new Set());
      this.selectedFile = null;
      this.step.set(1);
    }
  }

  protected importSites(): void {
    const file = this.selectedFile;

    if (
      !file ||
      this.configureForm.invalid ||
      this.selectedCount() === 0 ||
      this.submitting()
    ) {
      this.configureForm.markAllAsTouched();
      return;
    }

    const targetProjectId =
      this.configureForm.controls.targetProjectId.value;
    this.submitting.set(true);
    this.errorMessage.set(null);

    this.sitesApi
      .importSites(targetProjectId, file, {
        namePattern:
          this.configureForm.controls.namePattern.value.trim(),
        mode: this.configureForm.controls.mode.value,
        selectedFeatureIndexes: [
          ...this.selectedFeatureIndexes(),
        ],
      })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.step.set(4);
          this.completed.emit({
            targetProjectId,
            result,
          });
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'The GeoJSON could not be imported.',
            ),
          );
        },
      });
  }

  protected close(): void {
    if (
      this.submitting() ||
      (this.step() > 1 &&
        this.step() < 4 &&
        !window.confirm('Close the importer and discard this review?'))
    ) {
      return;
    }

    this.closed.emit();
  }

  protected done(): void {
    this.closed.emit();
  }

  protected viewOnMap(): void {
    const result = this.result();

    if (!result) {
      return;
    }

    this.closed.emit();
    void this.router.navigate([
      '/projects',
      result.projectId,
    ]);
  }

  protected modeDescription(): string {
    return this.configureForm.controls.mode.value === 'Merge'
      ? 'One Site will be created only if the selected polygons form a single compatible boundary.'
      : `${this.selectedPolygonCount()} polygon(s) will be saved as individual Sites.`;
  }

  private async readFile(file: File): Promise<void> {
    const extension = file.name
      .slice(file.name.lastIndexOf('.'))
      .toLocaleLowerCase();

    if (!['.geojson', '.json'].includes(extension)) {
      this.errorMessage.set(
        'Only .geojson and .json files are supported in this version.',
      );
      return;
    }

    if (file.size > SiteImportWizardComponent.maximumFileSize) {
      this.errorMessage.set(
        'The GeoJSON file cannot exceed 1 MB.',
      );
      return;
    }

    this.parsing.set(true);
    this.errorMessage.set(null);

    try {
      const content = await file.text();
      this.parseSource(file, content);
    } catch {
      this.errorMessage.set('The selected file could not be read.');
    } finally {
      this.parsing.set(false);
    }
  }

  private parseSource(file: File, content: string): void {
    try {
      const preview = parseGeoJsonImport(content, file.name);
      this.preview.set(preview);
      this.selectedFile = file;
      this.selectedFeatureIndexes.set(
        new Set(
          preview.features
            .filter(
              (feature) =>
                feature.issues.length === 0 &&
                feature.polygons.length > 0,
            )
            .map((feature) => feature.index),
        ),
      );
      this.errorMessage.set(null);
      this.step.set(2);
    } catch (error: unknown) {
      this.errorMessage.set(
        error instanceof GeoJsonPreviewError
          ? error.message
          : 'The GeoJSON could not be parsed.',
      );
    }
  }
}
