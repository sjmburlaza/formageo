export type LayerCategory =
  | 'Boundaries'
  | 'Planning'
  | 'Hazards'
  | 'Environment'
  | 'Terrain'
  | 'Transport'
  | 'Facilities';

export type LayerGeometryType = 'Point' | 'LineString' | 'Polygon' | 'Raster';

export type LayerDeliveryMethod =
  | 'GeoJson'
  | 'VectorTiles'
  | 'RasterTiles'
  | 'Wms'
  | 'Wmts';

export interface LayerDataSource {
  id: string;
  name: string;
  organization: string;
  licenseName: string;
  licenseUrl: string | null;
  attribution: string;
  sourceUrl: string | null;
}

export interface LayerVersion {
  id: string;
  versionLabel: string;
  lastUpdatedAtUtc: string;
  deliveryMethod: LayerDeliveryMethod;
  dataUrl: string;
  sourceLayer: string | null;
  minimumZoom: number | null;
  maximumZoom: number | null;
}

export interface LayerStyle {
  fillColor?: string;
  fillOpacity?: number;
  strokeColor?: string;
  strokeWidth?: number;
  lineColor?: string;
  lineWidth?: number;
  circleColor?: string;
  circleRadius?: number;
}

export interface LayerDefinition {
  id: string;
  name: string;
  description: string;
  category: LayerCategory;
  geographicCoverage: string;
  coordinateSystem: string;
  geometryType: LayerGeometryType;
  featureNameProperty: string;
  style: LayerStyle;
  dataSource: LayerDataSource;
  version: LayerVersion;
}

export interface LayerLegendItem {
  id: string;
  label: string;
  fillColor: string;
  strokeColor: string;
  symbol: string | null;
  sortOrder: number;
}

export interface LayerLegend {
  layerId: string;
  layerName: string;
  items: LayerLegendItem[];
}

export interface ProjectLayerPreference {
  layerId: string;
  isVisible: boolean;
  opacity: number;
  sortOrder: number;
  filter: string | null;
  updatedAtUtc?: string;
}

export interface UpdateProjectLayersRequest {
  layers: ProjectLayerPreference[];
}
