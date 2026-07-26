import { GeoJsonPosition } from './site.model';

export type ReportSourceType = 'Site' | 'Comparison' | 'Project';

export type ReportFormat =
  | 'Pdf'
  | 'MetricsCsv'
  | 'GeometryGeoJson'
  | 'AnalysisGeoJson'
  | 'ComparisonCsv'
  | 'ProjectArchive';

export type ReportSectionKey =
  | 'Cover'
  | 'ExecutiveSummary'
  | 'SiteLocation'
  | 'GeometrySummary'
  | 'HazardFindings'
  | 'PlanningFindings'
  | 'SuitabilityScore'
  | 'SiteComparison'
  | 'DataSources'
  | 'Disclaimer';

export interface ReportSectionSelection {
  key: ReportSectionKey;
  included: boolean;
  sortOrder: number;
}

export interface ReportBranding {
  organizationName: string;
  preparedBy: string;
  accentColor: string;
  footerText: string | null;
}

export interface CreateReportRequest {
  title: string;
  format: ReportFormat;
  sections: ReportSectionSelection[];
  branding: ReportBranding;
}

export interface GeneratedReport {
  id: string;
  projectId: string;
  sourceType: ReportSourceType;
  sourceId: string;
  format: ReportFormat;
  title: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  generatedAtUtc: string;
  sections: ReportSectionSelection[];
  branding: ReportBranding;
  downloadUrl: string;
}

export interface ReportPreview {
  sourceType: ReportSourceType;
  sourceId: string;
  projectId: string;
  projectName: string;
  sourceName: string;
  title: string;
  generationDateUtc: string;
  branding: ReportBranding;
  sections: ReportPreviewSection[];
  mapSites: ReportMapSite[];
  measurements: ReportMeasurement[];
  findings: ReportFinding[];
  suitabilityScore: ReportScore | null;
  comparison: ReportComparison | null;
  dataSources: ReportDataSource[];
  limitations: string[];
}

export interface ReportPreviewSection {
  key: ReportSectionKey;
  heading: string;
  summary: string;
  included: boolean;
  sortOrder: number;
}

export interface ReportMapSite {
  siteId: string;
  siteName: string;
  boundary: GeoJsonPosition[];
}

export interface ReportMeasurement {
  siteId: string;
  siteName: string;
  areaHectares: number | null;
  perimeterMetres: number | null;
  centroidLongitude: number | null;
  centroidLatitude: number | null;
  vertexCount: number;
  coordinateSystem: string;
}

export interface ReportFinding {
  siteId: string;
  siteName: string;
  category: string;
  name: string;
  severity: string;
  summary: string;
  classification: string;
  sitePercent: number | null;
  analysisVersion: string;
}

export interface ReportScoreMetric {
  name: string;
  rawValue: number | null;
  unit: string;
  normalizedScore: number | null;
  contribution: number;
  dataSource: string;
  dataVersion: string | null;
  explanation: string;
}

export interface ReportScore {
  siteId: string;
  siteName: string;
  overallScore: number | null;
  rating: string;
  isScoreable: boolean;
  modelVersion: number;
  modelName: string;
  calculatedAtUtc: string;
  metrics: ReportScoreMetric[];
  strengths: string[];
  weaknesses: string[];
  missingInformation: string[];
}

export interface ReportComparison {
  comparisonId: string;
  scenarioName: string;
  modelVersion: number;
  comparisonDateUtc: string;
  sites: ReportComparisonSite[];
  rankingExplanation: string;
}

export interface ReportComparisonSite {
  rank: number;
  siteId: string;
  siteName: string;
  overallScore: number | null;
  rating: string;
  metrics: ReportScoreMetric[];
}

export interface ReportDataSource {
  dataset: string;
  organization: string;
  attribution: string;
  version: string | null;
  publishedDate: string | null;
  license: string | null;
  sourceUrl: string | null;
}
