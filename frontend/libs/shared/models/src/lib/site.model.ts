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
