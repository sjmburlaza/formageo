export type GeoJsonPosition = [longitude: number, latitude: number];

export interface GeoJsonPolygon {
  type: 'Polygon';
  coordinates: GeoJsonPosition[][];
}

export interface Site {
  id: string;
  projectId: string;
  name: string;
  boundary: GeoJsonPolygon;
  createdAtUtc: string;
}

export interface CreateSiteRequest {
  name: string;
  boundary: GeoJsonPolygon;
}
