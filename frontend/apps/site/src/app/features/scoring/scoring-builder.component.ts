import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ScoringApiService,
  SitesApiService,
  getApiErrorMessage,
} from '@frontend/api-client';
import { MapComponent, MapFeature } from '@frontend/map';
import {
  MissingDataBehavior,
  NormalizationMethod,
  SaveScoringCriterion,
  SaveScoringScenarioRequest,
  ScoringCatalog,
  ScoringCriterion,
  ScoringCriterionDefinition,
  ScoringDirection,
  ScoringPreset,
  ScoringResult,
  ScoringScenario,
  Site,
} from '@frontend/models';
import {
  LucideArrowLeft,
  LucideChartNoAxesCombined,
  LucideCheck,
  LucideChevronDown,
  LucideCircleAlert,
  LucideCircleHelp,
  LucideCopy,
  LucideFlaskConical,
  LucideInfo,
  LucideMap,
  LucidePlus,
  LucideSave,
  LucideSparkles,
  LucideTrash2,
} from '@lucide/angular';
import { finalize, forkJoin } from 'rxjs';

type EditableCriterion = SaveScoringCriterion & {
  description: string;
  requiredAnalysis: string;
  recommendedDirection: ScoringDirection;
};

@Component({
  selector: 'fg-scoring-builder',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LucideArrowLeft,
    LucideChartNoAxesCombined,
    LucideCheck,
    LucideChevronDown,
    LucideCircleAlert,
    LucideCircleHelp,
    LucideCopy,
    LucideFlaskConical,
    LucideInfo,
    LucideMap,
    LucidePlus,
    LucideSave,
    LucideSparkles,
    LucideTrash2,
    MapComponent,
    RouterLink,
  ],
  templateUrl: './scoring-builder.component.html',
  styleUrl: './scoring-builder.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScoringBuilderComponent implements OnInit {
  protected readonly Math = Math;

  private readonly route = inject(ActivatedRoute);
  private readonly sitesApi = inject(SitesApiService);
  private readonly scoringApi = inject(ScoringApiService);

  protected readonly site = signal<Site | null>(null);
  protected readonly projectSites = signal<Site[]>([]);
  protected readonly catalog = signal<ScoringCatalog | null>(null);
  protected readonly scenarios = signal<ScoringScenario[]>([]);
  protected readonly activeScenarioId = signal<string | null>(null);
  protected readonly scenarioName = signal('Balanced site screening');
  protected readonly criteria = signal<EditableCriterion[]>([]);
  protected readonly selectedSiteIds = signal<ReadonlySet<string>>(new Set());
  protected readonly results = signal<ScoringResult[]>([]);
  protected readonly selectedResultSiteId = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly running = signal(false);
  protected readonly historyLoading = signal(false);
  protected readonly dirty = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly noticeMessage = signal<string | null>(null);
  protected readonly showCriterionPicker = signal(false);

  protected readonly weightTotal = computed(() =>
    this.criteria().reduce(
      (total, criterion) => total + Number(criterion.weight || 0),
      0,
    ),
  );
  protected readonly weightDifference = computed(
    () => 100 - this.weightTotal(),
  );
  protected readonly thresholdErrors = computed(() =>
    this.criteria()
      .filter(
        (criterion) =>
          !Number.isFinite(criterion.lowerThreshold) ||
          !Number.isFinite(criterion.upperThreshold) ||
          criterion.lowerThreshold >= criterion.upperThreshold,
      )
      .map(
        (criterion) =>
          `${criterion.name}: the upper threshold must be greater than the lower threshold.`,
      ),
  );
  protected readonly conflictWarnings = computed(() =>
    this.criteria()
      .filter(
        (criterion) => criterion.direction !== criterion.recommendedDirection,
      )
      .map(
        (criterion) =>
          `${criterion.name} is reversed from the catalog recommendation; verify that this is intentional.`,
      ),
  );
  protected readonly formValid = computed(
    () =>
      this.scenarioName().trim().length > 0 &&
      this.criteria().length > 0 &&
      Math.abs(this.weightDifference()) < 0.001 &&
      this.thresholdErrors().length === 0,
  );
  protected readonly activeModelVersion = computed(
    () =>
      this.scenarios().find(
        (scenario) => scenario.id === this.activeScenarioId(),
      )?.latestModel.version ?? null,
  );
  protected readonly availableCriteria = computed(() => {
    const selectedKeys = new Set(
      this.criteria().map((criterion) => criterion.key),
    );
    return (
      this.catalog()?.criteria.filter(
        (criterion) => !selectedKeys.has(criterion.key),
      ) ?? []
    );
  });
  protected readonly latestResults = computed(() => {
    const latestBySite = new Map<string, ScoringResult>();
    const activeVersion = this.activeModelVersion();
    for (const result of this.results()) {
      if (activeVersion !== null && result.modelVersion !== activeVersion) {
        continue;
      }
      if (!latestBySite.has(result.siteId)) {
        latestBySite.set(result.siteId, result);
      }
    }
    return [...latestBySite.values()].sort((left, right) => {
      if (left.overallScore === null) return 1;
      if (right.overallScore === null) return -1;
      return right.overallScore - left.overallScore;
    });
  });
  protected readonly selectedResult = computed(
    () =>
      this.latestResults().find(
        (result) => result.siteId === this.selectedResultSiteId(),
      ) ??
      this.latestResults()[0] ??
      null,
  );
  protected readonly resultMapFeatures = computed<MapFeature[]>(() => {
    const resultIds = new Set(
      this.latestResults().map((result) => result.siteId),
    );
    return this.projectSites()
      .filter((site) => resultIds.has(site.id))
      .map((site) => {
        const result = this.latestResults().find(
          (item) => item.siteId === site.id,
        );
        return {
          id: site.id,
          geometry: site.boundary,
          properties: {
            name: site.name,
            score: result?.overallScore ?? 'Not scoreable',
            rating: result?.rating ?? '',
          },
        };
      });
  });

  ngOnInit(): void {
    const siteId = this.route.snapshot.paramMap.get('siteId');
    if (!siteId) {
      this.loading.set(false);
      this.errorMessage.set('No site ID was provided.');
      return;
    }

    forkJoin({
      site: this.sitesApi.getSite(siteId),
      catalog: this.scoringApi.getCatalog(),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ site, catalog }) => {
          this.site.set(site);
          this.catalog.set(catalog);
          this.selectedSiteIds.set(new Set([site.id]));
          this.applyPreset(catalog.presets[0], false);
          this.loadProjectData(site.projectId);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'The suitability scoring workspace could not be loaded.',
            ),
          );
        },
      });
  }

  protected applyPreset(
    preset: ScoringPreset | undefined,
    markDirty = true,
    resetScenario = false,
  ): void {
    const catalog = this.catalog();
    if (!preset || !catalog) return;

    const criteria = preset.criteria
      .map((presetCriterion) => {
        const definition = catalog.criteria.find(
          (item) => item.key === presetCriterion.criterionKey,
        );
        return definition
          ? this.fromDefinition(definition, presetCriterion.weight)
          : null;
      })
      .filter(
        (criterion): criterion is EditableCriterion => criterion !== null,
      );

    this.criteria.set(criteria);
    this.scenarioName.set(preset.name);
    if (resetScenario) {
      this.activeScenarioId.set(null);
    }
    this.results.set([]);
    this.selectedResultSiteId.set(null);
    this.dirty.set(markDirty);
    this.noticeMessage.set(
      markDirty
        ? `${preset.name} preset applied. Review thresholds before saving.`
        : null,
    );
  }

  protected newScenario(): void {
    this.applyPreset(this.catalog()?.presets[0], false, true);
    this.scenarioName.set('Untitled scoring scenario');
    this.dirty.set(false);
    this.noticeMessage.set('New scenario ready to configure.');
  }

  protected selectScenario(scenarioId: string): void {
    const scenario = this.scenarios().find((item) => item.id === scenarioId);
    if (!scenario) return;

    this.activeScenarioId.set(scenario.id);
    this.scenarioName.set(scenario.name);
    this.criteria.set(
      [...scenario.latestModel.criteria]
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map((criterion) => this.fromSavedCriterion(criterion)),
    );
    this.results.set([]);
    this.selectedResultSiteId.set(this.site()?.id ?? null);
    this.dirty.set(false);
    this.noticeMessage.set(
      `Loaded version ${scenario.latestModel.version}. Saving changes will create a new version.`,
    );
    this.loadResults(scenario.id);
  }

  protected setScenarioName(name: string): void {
    this.scenarioName.set(name);
    this.markDirty();
  }

  protected addCriterion(definition: ScoringCriterionDefinition): void {
    const remainingWeight = Math.max(0, 100 - this.weightTotal());
    this.criteria.update((criteria) => [
      ...criteria,
      this.fromDefinition(
        definition,
        remainingWeight > 0 ? remainingWeight : 10,
      ),
    ]);
    this.showCriterionPicker.set(false);
    this.markDirty();
  }

  protected removeCriterion(key: string): void {
    this.criteria.update((criteria) =>
      criteria.filter((criterion) => criterion.key !== key),
    );
    this.markDirty();
  }

  protected updateWeight(key: string, value: string): void {
    this.updateCriterion(key, {
      weight: this.numberValue(value),
    });
  }

  protected updateDirection(key: string, value: ScoringDirection): void {
    this.updateCriterion(key, { direction: value });
  }

  protected updateNormalization(key: string, value: NormalizationMethod): void {
    this.updateCriterion(key, {
      normalizationMethod: value,
    });
  }

  protected updateLowerThreshold(key: string, value: string): void {
    this.updateCriterion(key, {
      lowerThreshold: this.numberValue(value),
    });
  }

  protected updateUpperThreshold(key: string, value: string): void {
    this.updateCriterion(key, {
      upperThreshold: this.numberValue(value),
    });
  }

  protected updateMissingBehavior(
    key: string,
    value: MissingDataBehavior,
  ): void {
    this.updateCriterion(key, {
      missingDataBehavior: value,
    });
  }

  protected toggleComparisonSite(siteId: string, checked: boolean): void {
    this.selectedSiteIds.update((selected) => {
      const next = new Set(selected);
      if (checked) {
        next.add(siteId);
      } else {
        next.delete(siteId);
      }
      return next;
    });
  }

  protected saveScenario(): void {
    if (!this.formValid() || this.saving()) return;
    this.persistScenario();
  }

  protected duplicateScenario(): void {
    const scenarioId = this.activeScenarioId();
    if (!scenarioId || this.saving()) return;

    this.saving.set(true);
    this.errorMessage.set(null);
    this.scoringApi
      .duplicateScenario(scenarioId)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (scenario) => {
          this.scenarios.update((scenarios) => [scenario, ...scenarios]);
          this.selectScenario(scenario.id);
          this.noticeMessage.set(
            'Scenario duplicated as an independent model.',
          );
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The scenario could not be duplicated.'),
          );
        },
      });
  }

  protected runScoring(): void {
    if (
      !this.formValid() ||
      this.running() ||
      this.selectedSiteIds().size === 0
    ) {
      return;
    }

    if (this.dirty() || !this.activeScenarioId()) {
      this.persistScenario((scenario) => this.executeScoring(scenario));
      return;
    }

    const scenario = this.scenarios().find(
      (item) => item.id === this.activeScenarioId(),
    );
    if (scenario) this.executeScoring(scenario);
  }

  protected selectResult(siteId: string): void {
    this.selectedResultSiteId.set(siteId);
  }

  protected scoreTone(score: number | null): string {
    if (score === null) return 'score--missing';
    if (score >= 80) return 'score--excellent';
    if (score >= 65) return 'score--good';
    if (score >= 50) return 'score--moderate';
    return 'score--weak';
  }

  protected formatValue(value: number | null, unit: string): string {
    return value === null
      ? 'Missing'
      : `${value.toLocaleString(undefined, {
          maximumFractionDigits: 2,
        })}${unit}`;
  }

  private loadProjectData(projectId: string): void {
    forkJoin({
      sites: this.sitesApi.getProjectSites(projectId),
      scenarios: this.scoringApi.getProjectScenarios(projectId),
    }).subscribe({
      next: ({ sites, scenarios }) => {
        this.projectSites.set(sites);
        this.scenarios.set(scenarios);
      },
      error: (error: unknown) => {
        this.errorMessage.set(
          getApiErrorMessage(
            error,
            'Saved scenarios and comparison sites could not be loaded.',
          ),
        );
      },
    });
  }

  private loadResults(scenarioId: string): void {
    this.historyLoading.set(true);
    this.scoringApi
      .getResults(scenarioId)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: (results) => {
          this.results.set(results);
          if (!this.selectedResultSiteId()) {
            this.selectedResultSiteId.set(
              this.site()?.id ?? results[0]?.siteId ?? null,
            );
          }
        },
        error: () => {
          this.noticeMessage.set(
            'The scenario loaded, but its previous score history is unavailable.',
          );
        },
      });
  }

  private persistScenario(onSaved?: (scenario: ScoringScenario) => void): void {
    const site = this.site();
    if (!site) return;

    const request: SaveScoringScenarioRequest = {
      name: this.scenarioName().trim(),
      criteria: this.criteria().map((criterion) => ({
        key: criterion.key,
        name: criterion.name,
        weight: criterion.weight,
        direction: criterion.direction,
        normalizationMethod: criterion.normalizationMethod,
        dataSource: criterion.dataSource,
        unit: criterion.unit,
        lowerThreshold: criterion.lowerThreshold,
        upperThreshold: criterion.upperThreshold,
        missingDataBehavior: criterion.missingDataBehavior,
      })),
    };
    const activeId = this.activeScenarioId();
    const saveRequest = activeId
      ? this.scoringApi.updateScenario(activeId, request)
      : this.scoringApi.createScenario(site.projectId, request);

    this.saving.set(true);
    this.errorMessage.set(null);
    saveRequest.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (scenario) => {
        this.activeScenarioId.set(scenario.id);
        this.scenarios.update((scenarios) => [
          scenario,
          ...scenarios.filter((item) => item.id !== scenario.id),
        ]);
        this.criteria.set(
          scenario.latestModel.criteria.map((criterion) =>
            this.fromSavedCriterion(criterion),
          ),
        );
        this.dirty.set(false);
        this.noticeMessage.set(
          `Scenario saved as model version ${scenario.latestModel.version}.`,
        );
        onSaved?.(scenario);
      },
      error: (error: unknown) => {
        this.errorMessage.set(
          getApiErrorMessage(error, 'The scoring scenario could not be saved.'),
        );
      },
    });
  }

  private executeScoring(scenario: ScoringScenario): void {
    this.running.set(true);
    this.errorMessage.set(null);
    this.scoringApi
      .runScoring(scenario.id, {
        siteIds: [...this.selectedSiteIds()],
        modelVersion: scenario.latestModel.version,
      })
      .pipe(finalize(() => this.running.set(false)))
      .subscribe({
        next: (results) => {
          this.results.set(results);
          this.selectedResultSiteId.set(
            this.site()?.id ?? results[0]?.siteId ?? null,
          );
          const unscoreable = results.filter(
            (result) => !result.isScoreable,
          ).length;
          this.noticeMessage.set(
            unscoreable > 0
              ? `Scoring finished. ${unscoreable} ${
                  unscoreable === 1 ? 'site could' : 'sites could'
                } not be scored because required data is missing.`
              : `Scoring finished for ${results.length} ${
                  results.length === 1 ? 'site' : 'sites'
                } using model version ${scenario.latestModel.version}.`,
          );
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(
              error,
              'Suitability scoring could not be completed.',
            ),
          );
        },
      });
  }

  private updateCriterion(
    key: string,
    change: Partial<EditableCriterion>,
  ): void {
    this.criteria.update((criteria) =>
      criteria.map((criterion) =>
        criterion.key === key ? { ...criterion, ...change } : criterion,
      ),
    );
    this.markDirty();
  }

  private markDirty(): void {
    this.dirty.set(true);
    this.noticeMessage.set(null);
  }

  private numberValue(value: string): number {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
  }

  private fromDefinition(
    definition: ScoringCriterionDefinition,
    weight: number,
  ): EditableCriterion {
    return {
      key: definition.key,
      name: definition.name,
      description: definition.description,
      weight,
      direction: definition.recommendedDirection,
      recommendedDirection: definition.recommendedDirection,
      normalizationMethod: definition.recommendedNormalizationMethod,
      dataSource: definition.dataSource,
      unit: definition.unit,
      lowerThreshold: definition.defaultLowerThreshold,
      upperThreshold: definition.defaultUpperThreshold,
      missingDataBehavior: definition.defaultMissingDataBehavior,
      requiredAnalysis: definition.requiredAnalysis,
    };
  }

  private fromSavedCriterion(criterion: ScoringCriterion): EditableCriterion {
    const definition = this.catalog()?.criteria.find(
      (item) => item.key === criterion.key,
    );
    return {
      key: criterion.key,
      name: criterion.name,
      description: definition?.description ?? 'Custom scoring criterion.',
      weight: criterion.weight,
      direction: criterion.direction,
      recommendedDirection:
        definition?.recommendedDirection ?? criterion.direction,
      normalizationMethod: criterion.normalizationMethod,
      dataSource: criterion.dataSource,
      unit: criterion.unit,
      lowerThreshold: criterion.lowerThreshold,
      upperThreshold: criterion.upperThreshold,
      missingDataBehavior: criterion.missingDataBehavior,
      requiredAnalysis: definition?.requiredAnalysis ?? 'Custom source',
    };
  }
}
