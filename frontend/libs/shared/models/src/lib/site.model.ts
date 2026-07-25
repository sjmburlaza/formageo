export type GeoJsonPosition = [longitude: number, latitude: number];

export interface GeoJsonPolygon {
  type: 'Polygon';
  coordinates: GeoJsonPosition[][];
}

export type SiteStatus = 'Draft' | 'Active' | 'Archived';

export interface Site {
  id: string;
  projectId: string;
  name: string;
  boundary: GeoJsonPolygon;
  createdAtUtc: string;
  updatedAtUtc: string;
  status: SiteStatus;
  archivedAtUtc: string | null;
}

export interface CreateSiteRequest {
  name: string;
  boundary: GeoJsonPolygon;
}

export interface UpdateSiteRequest {
  name: string;
}

export interface UpdateSiteBoundaryRequest {
  boundary: GeoJsonPolygon;
}

export type SiteImportMode = 'Separate' | 'Merge';

export interface SiteImportOptions {
  namePattern: string;
  mode: SiteImportMode;
  selectedFeatureIndexes: number[];
}

export interface SiteImportSkippedFeature {
  featureIndex: number | null;
  featureName: string | null;
  reason: string;
}

export interface SiteImportResult {
  importId: string;
  projectId: string;
  featureCount: number;
  invalidFeatureCount: number;
  detectedCoordinateSystem: string;
  importedSites: Site[];
  skippedFeatures: SiteImportSkippedFeature[];
  warnings: string[];
}

export interface SiteSummary {
  siteId: string;
  geometry: SiteGeometrySummary;
  measurements: SiteMeasurementsSummary;
  location: SiteLocationSummary;
  dataQualityWarnings: string[];
}

export interface SiteGeometrySummary {
  type: string;
  srid: number;
  coordinateSystem: string;
  isValid: boolean;
  validityReason: string;
  ringCount: number;
  vertexCount: number;
}

export interface SiteMeasurementsSummary {
  areaSquareMetres: number | null;
  areaHectares: number | null;
  perimeterMetres: number | null;
  calculationMethod: string;
}

export interface SiteLocationSummary {
  centroid: SiteCoordinate | null;
  boundingBox: SiteBoundingBox;
}

export interface SiteCoordinate {
  longitude: number;
  latitude: number;
}

export interface SiteBoundingBox {
  west: number;
  south: number;
  east: number;
  north: number;
}
