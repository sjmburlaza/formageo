import { CriterionScore, ScoringDirection } from './scoring.model';

export interface CreateComparisonRequest {
  scoringScenarioId: string;
  siteIds: string[];
  metricKeys: string[];
}

export interface ComparisonMetric {
  key: string;
  name: string;
  unit: string;
  direction: ScoringDirection;
  dataSource: string;
  configuredWeight: number;
}

export interface ComparisonDataVersion {
  criterionKey: string;
  dataSource: string;
  versions: string[];
}

export interface ComparisonRanking {
  rank: number;
  siteId: string;
  siteName: string;
  overallScore: number | null;
  rating: string;
  isScoreable: boolean;
  metrics: CriterionScore[];
  strengths: string[];
  weaknesses: string[];
  missingInformation: string[];
}

export interface SiteComparison {
  id: string;
  projectId: string;
  scoringScenarioId: string;
  scoringModelId: string;
  scenarioName: string;
  modelVersion: number;
  modelName: string;
  selectedSiteIds: string[];
  metrics: ComparisonMetric[];
  rankings: ComparisonRanking[];
  comparisonDateUtc: string;
  dataVersions: ComparisonDataVersion[];
  rankingExplanation: string;
}

export interface ComparisonSummary {
  id: string;
  projectId: string;
  scoringScenarioId: string;
  scenarioName: string;
  modelVersion: number;
  siteCount: number;
  leadingSiteName: string | null;
  leadingScore: number | null;
  comparisonDateUtc: string;
}
