import {
  LayerCategory,
  LayerDataSource,
  LayerDeliveryMethod,
  LayerGeometryType,
  LayerLegendItem,
  LayerStyle,
} from '@frontend/models';

export interface BaseMapDefinition {
  id: string;
  label: string;
}

export interface MapOverlay {
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
  lastUpdatedAtUtc: string;
  deliveryMethod: LayerDeliveryMethod;
  dataUrl: string;
  sourceLayer: string | null;
  minimumZoom: number | null;
  maximumZoom: number | null;
  legend: LayerLegendItem[];
  visible: boolean;
  opacity: number;
  sortOrder: number;
  filter: string | null;
}

export interface MapOverlayStateChange {
  layerId: string;
  visible: boolean;
  opacity: number;
  sortOrder: number;
  filter: string | null;
}

export interface MapIdentifiedFeature {
  layerId: string;
  layerName: string;
  featureName: string;
  attributes: ReadonlyArray<{
    label: string;
    value: string;
  }>;
  source: LayerDataSource;
  lastUpdatedAtUtc: string;
}
