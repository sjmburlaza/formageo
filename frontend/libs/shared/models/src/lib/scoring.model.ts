export type ScoringDirection = 'HigherIsBetter' | 'LowerIsBetter';

export type NormalizationMethod = 'Linear' | 'Threshold' | 'Boolean';

export type MissingDataBehavior =
  | 'ExcludeAndReweight'
  | 'ScoreZero'
  | 'CannotScore';

export interface ScoringCriterionDefinition {
  key: string;
  name: string;
  description: string;
  recommendedDirection: ScoringDirection;
  recommendedNormalizationMethod: NormalizationMethod;
  dataSource: string;
  unit: string;
  defaultLowerThreshold: number;
  defaultUpperThreshold: number;
  defaultMissingDataBehavior: MissingDataBehavior;
  requiredAnalysis: string;
}

export interface ScoringPresetCriterion {
  criterionKey: string;
  weight: number;
}

export interface ScoringPreset {
  key: string;
  name: string;
  description: string;
  criteria: ScoringPresetCriterion[];
}

export interface ScoringCatalog {
  requiredWeightTotal: number;
  criteria: ScoringCriterionDefinition[];
  presets: ScoringPreset[];
}

export interface SaveScoringCriterion {
  key: string;
  name: string;
  weight: number;
  direction: ScoringDirection;
  normalizationMethod: NormalizationMethod;
  dataSource: string;
  unit: string;
  lowerThreshold: number;
  upperThreshold: number;
  missingDataBehavior: MissingDataBehavior;
}

export interface SaveScoringScenarioRequest {
  name: string;
  criteria: SaveScoringCriterion[];
}

export interface ScoringCriterion extends SaveScoringCriterion {
  id: string;
  sortOrder: number;
}

export interface ScoringModel {
  id: string;
  scoringScenarioId: string;
  version: number;
  name: string;
  createdAtUtc: string;
  criteria: ScoringCriterion[];
}

export interface ScoringScenario {
  id: string;
  projectId: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  latestModel: ScoringModel;
}

export interface RunScoringRequest {
  siteIds: string[];
  modelVersion?: number;
}

export interface CriterionScore {
  criterionKey: string;
  name: string;
  rawValue: number | null;
  unit: string;
  normalizedScore: number | null;
  configuredWeight: number;
  effectiveWeight: number;
  contribution: number;
  dataSource: string;
  dataVersion: string | null;
  isMissing: boolean;
  missingDataBehavior: MissingDataBehavior;
  explanation: string;
}

export interface ScoringResult {
  id: string;
  siteId: string;
  siteName: string;
  scoringScenarioId: string;
  scoringModelId: string;
  modelVersion: number;
  modelName: string;
  overallScore: number | null;
  rating: string;
  isScoreable: boolean;
  calculatedAtUtc: string;
  configuredWeightTotal: number;
  effectiveWeightTotal: number;
  criteria: CriterionScore[];
  strengths: string[];
  weaknesses: string[];
  missingInformation: string[];
  validationMessages: string[];
}
