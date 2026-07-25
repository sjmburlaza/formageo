export type AnalysisType =
  | 'SiteGeometry'
  | 'HazardExposure'
  | 'Zoning'
  | 'Accessibility'
  | 'NearbyFacilities'
  | 'Suitability';

export type AnalysisStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface AnalysisParameterDefinition {
  name: string;
  label: string;
  description: string;
  type: 'number' | 'boolean' | 'text';
  required: boolean;
  defaultValue: unknown;
  minimum: number | null;
  maximum: number | null;
}

export interface AnalysisDefinition {
  analysisType: AnalysisType;
  name: string;
  description: string;
  requiredInputs: string[];
  estimatedComplexity: string;
  analysisVersion: string;
  parameters: AnalysisParameterDefinition[];
}

export interface AnalysisResult {
  summary?: string;
  metrics?: Record<string, unknown>;
  resultGeometry?: GeoJsonResult | null;
  [key: string]: unknown;
}

export type GeoJsonResult =
  | {
      type: 'Feature';
      properties: Record<string, unknown>;
      geometry: Record<string, unknown>;
    }
  | {
      type: 'FeatureCollection';
      features: unknown[];
    };

export interface AnalysisRun {
  id: string;
  siteId: string;
  analysisType: AnalysisType;
  status: AnalysisStatus;
  inputParameters: Record<string, unknown>;
  result: AnalysisResult | null;
  errorMessage: string | null;
  requestedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  analysisVersion: string;
}

export interface CreateAnalysisRequest {
  analysisType: AnalysisType;
  inputParameters: Record<string, unknown>;
}
