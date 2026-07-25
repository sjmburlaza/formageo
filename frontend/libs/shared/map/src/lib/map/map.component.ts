import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnDestroy,
  ViewChild,
  ViewEncapsulation,
  output,
  signal,
} from '@angular/core';
import type {
  Feature,
  FeatureCollection,
  GeoJsonProperties,
  LineString,
  Point,
  Polygon,
} from 'geojson';
import {
  GeoJSONSource,
  LngLatBounds,
  Map as MapLibreMap,
  MapLayerMouseEvent,
  MapMouseEvent,
  NavigationControl,
  ScaleControl,
  setWorkerUrl,
  StyleSpecification,
} from 'maplibre-gl';

export type MapPosition = [longitude: number, latitude: number];

export interface MapPolygonGeometry {
  type: 'Polygon';
  coordinates: MapPosition[][];
}

export interface MapFeature {
  id: string;
  geometry: MapPolygonGeometry;
  properties?: Record<string, string | number | boolean | null>;
  visible?: boolean;
}

export interface MapGeometryEvent {
  featureId?: string;
  geometry: MapPolygonGeometry;
}

export interface MapFeatureSelectionEvent {
  featureId: string;
}

export type MapInteractionMode = 'idle' | 'drawing' | 'editing';

interface BaseMapDefinition {
  id: string;
  label: string;
}

const FEATURES_SOURCE = 'fg-features';
const DRAFT_SOURCE = 'fg-draft';
const VERTICES_SOURCE = 'fg-vertices';
const FEATURE_FILL_LAYER = 'fg-feature-fill';
const FEATURE_OUTLINE_LAYER = 'fg-feature-outline';
const SELECTED_FILL_LAYER = 'fg-selected-fill';
const SELECTED_OUTLINE_LAYER = 'fg-selected-outline';
const DRAFT_FILL_LAYER = 'fg-draft-fill';
const DRAFT_LINE_LAYER = 'fg-draft-line';
const DRAFT_VERTEX_LAYER = 'fg-draft-vertices';
const EDIT_VERTEX_LAYER = 'fg-edit-vertices';

const EMPTY_COLLECTION: FeatureCollection = {
  type: 'FeatureCollection',
  features: [],
};

@Component({
  selector: 'fg-map',
  standalone: true,
  imports: [],
  templateUrl: './map.component.html',
  styleUrl: './map.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class MapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('mapCanvas', { static: true })
  private readonly mapCanvas!: ElementRef<HTMLDivElement>;

  private map?: MapLibreMap;
  private resizeObserver?: ResizeObserver;
  private mapFeatures: MapFeature[] = [];
  private selectedFeatureIdValue: string | null = null;
  private draftPositions: MapPosition[] = [];
  private activeVertexIndex: number | null = null;
  private hasAutoFittedFeatures = false;

  protected readonly mode = signal<MapInteractionMode>('idle');
  protected readonly selectedFeatureIdSignal = signal<string | null>(null);
  protected readonly activeBaseMap = signal('streets');
  protected readonly mapReady = signal(false);
  protected readonly baseMaps: BaseMapDefinition[] = [
    { id: 'streets', label: 'Streets' },
    { id: 'light', label: 'Light' },
    { id: 'satellite', label: 'Satellite' },
  ];

  readonly geometryCreated = output<MapGeometryEvent>();
  readonly geometryEdited = output<MapGeometryEvent>();
  readonly geometryDeleted = output<MapFeatureSelectionEvent>();
  readonly featureSelected = output<MapFeatureSelectionEvent>();
  readonly modeChanged = output<MapInteractionMode>();

  @Input() workerUrl = '/assets/maplibre/maplibre-gl-worker.mjs';

  @Input()
  set features(value: MapFeature[] | null | undefined) {
    this.mapFeatures = (value ?? []).map((feature) => ({
      ...feature,
      geometry: this.cloneGeometry(feature.geometry),
    }));
    this.updateFeatureSource();
    this.updateEditVertices();
  }

  @Input()
  set selectedFeatureId(value: string | null | undefined) {
    this.selectedFeatureIdValue = value ?? null;
    this.selectedFeatureIdSignal.set(this.selectedFeatureIdValue);
    this.updateSelectionLayers();
    this.updateEditVertices();
  }

  ngAfterViewInit(): void {
    this.initializeMap();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.map?.remove();
  }

  startDrawing(): void {
    if (!this.map) {
      return;
    }

    this.setMode('drawing');
    this.activeVertexIndex = null;
    this.draftPositions = [];
    this.updateDraftSource();
    this.updateEditVertices();
    this.map.doubleClickZoom.disable();
    this.map.getCanvas().focus();
  }

  cancelDrawing(): void {
    this.draftPositions = [];
    this.updateDraftSource();
    this.finishInteraction();
  }

  clearDraft(): void {
    this.draftPositions = [];
    this.updateDraftSource();
  }

  editSelected(): void {
    if (!this.selectedFeatureIdValue || !this.getSelectedFeature()) {
      return;
    }

    this.clearDraft();
    this.setMode('editing');
    this.updateEditVertices();
    this.map?.getCanvas().focus();
  }

  stopEditing(): void {
    this.activeVertexIndex = null;
    this.finishInteraction();
  }

  requestSelectedFeatureRemoval(): void {
    if (!this.selectedFeatureIdValue) {
      return;
    }

    this.geometryDeleted.emit({
      featureId: this.selectedFeatureIdValue,
    });
  }

  fitToGeometry(
    geometry: MapPolygonGeometry,
    options: { padding?: number; maxZoom?: number } = {},
  ): void {
    if (!this.map) {
      return;
    }

    const positions = geometry.coordinates.flat();

    if (positions.length === 0) {
      return;
    }

    const bounds = positions.reduce(
      (currentBounds, position) => currentBounds.extend(position),
      new LngLatBounds(positions[0], positions[0]),
    );

    this.map.fitBounds(bounds, {
      padding: options.padding ?? 72,
      maxZoom: options.maxZoom ?? 17,
      duration: 650,
    });
  }

  resize(): void {
    this.map?.resize();
  }

  protected changeBaseMap(event: Event): void {
    const baseMapId = (event.target as HTMLSelectElement).value;
    this.activeBaseMap.set(baseMapId);

    for (const baseMap of this.baseMaps) {
      this.map?.setLayoutProperty(
        `fg-basemap-${baseMap.id}`,
        'visibility',
        baseMap.id === baseMapId ? 'visible' : 'none',
      );
    }
  }

  protected handleKeydown(event: KeyboardEvent): void {
    if (this.mode() === 'drawing') {
      if (event.key === 'Escape') {
        event.preventDefault();
        this.cancelDrawing();
      } else if (event.key === 'Enter') {
        event.preventDefault();
        this.completeDrawing();
      } else if (
        (event.key === 'Backspace' || event.key === 'Delete') &&
        this.draftPositions.length > 0
      ) {
        event.preventDefault();
        this.draftPositions.pop();
        this.updateDraftSource();
      }
    } else if (this.mode() === 'editing' && event.key === 'Escape') {
      event.preventDefault();
      this.stopEditing();
    }
  }

  private initializeMap(): void {
    if (this.workerUrl) {
      setWorkerUrl(this.workerUrl);
    }

    this.map = new MapLibreMap({
      container: this.mapCanvas.nativeElement,
      style: this.createStyle(),
      center: [121.0244, 14.5547],
      zoom: 11,
      attributionControl: {},
      cooperativeGestures: true,
    });

    this.map.addControl(
      new NavigationControl({
        showCompass: false,
        visualizePitch: false,
      }),
      'top-right',
    );
    this.map.addControl(
      new ScaleControl({
        maxWidth: 110,
        unit: 'metric',
      }),
      'bottom-left',
    );

    this.map.on('load', () => {
      this.addGeometryLayers();
      this.registerMapEvents();
      this.updateDraftSource();
      this.updateSelectionLayers();
      this.fitToVisibleFeatures();
      this.mapReady.set(true);
    });
    this.resizeObserver = new ResizeObserver(() => this.map?.resize());
    this.resizeObserver.observe(this.mapCanvas.nativeElement);
  }

  private createStyle(): StyleSpecification {
    return {
      version: 8,
      sources: {
        'fg-basemap-streets-source': {
          type: 'raster',
          tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
          tileSize: 256,
          attribution:
            '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        },
        'fg-basemap-light-source': {
          type: 'raster',
          tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
          tileSize: 256,
          attribution:
            '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        },
        'fg-basemap-satellite-source': {
          type: 'raster',
          tiles: [
            'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
          ],
          tileSize: 256,
          attribution: 'Tiles &copy; Esri',
        },
      },
      layers: [
        {
          id: 'fg-basemap-streets',
          type: 'raster',
          source: 'fg-basemap-streets-source',
        },
        {
          id: 'fg-basemap-light',
          type: 'raster',
          source: 'fg-basemap-light-source',
          layout: { visibility: 'none' },
          paint: {
            'raster-saturation': -0.9,
            'raster-contrast': -0.2,
            'raster-brightness-min': 0.22,
            'raster-brightness-max': 0.92,
          },
        },
        {
          id: 'fg-basemap-satellite',
          type: 'raster',
          source: 'fg-basemap-satellite-source',
          layout: { visibility: 'none' },
        },
      ],
    };
  }

  private addGeometryLayers(): void {
    if (!this.map) {
      return;
    }

    const featureCollection = this.createFeatureCollection();
    this.map.addSource(FEATURES_SOURCE, {
      type: 'geojson',
      data: featureCollection,
    });
    this.map.addSource(DRAFT_SOURCE, {
      type: 'geojson',
      data: EMPTY_COLLECTION,
    });
    this.map.addSource(VERTICES_SOURCE, {
      type: 'geojson',
      data: EMPTY_COLLECTION,
    });

    this.map.addLayer({
      id: FEATURE_FILL_LAYER,
      type: 'fill',
      source: FEATURES_SOURCE,
      paint: {
        'fill-color': '#2f7d4b',
        'fill-opacity': 0.38,
      },
    });
    this.map.addLayer({
      id: FEATURE_OUTLINE_LAYER,
      type: 'line',
      source: FEATURES_SOURCE,
      paint: {
        'line-color': '#21633a',
        'line-width': 2,
      },
    });
    this.map.addLayer({
      id: SELECTED_FILL_LAYER,
      type: 'fill',
      source: FEATURES_SOURCE,
      filter: ['==', ['get', 'id'], ''],
      paint: {
        'fill-color': '#d7922d',
        'fill-opacity': 0.52,
      },
    });
    this.map.addLayer({
      id: SELECTED_OUTLINE_LAYER,
      type: 'line',
      source: FEATURES_SOURCE,
      filter: ['==', ['get', 'id'], ''],
      paint: {
        'line-color': '#9c5c0a',
        'line-width': 3,
      },
    });
    this.map.addLayer({
      id: DRAFT_FILL_LAYER,
      type: 'fill',
      source: DRAFT_SOURCE,
      filter: ['==', '$type', 'Polygon'],
      paint: {
        'fill-color': '#21835a',
        'fill-opacity': 0.22,
      },
    });
    this.map.addLayer({
      id: DRAFT_LINE_LAYER,
      type: 'line',
      source: DRAFT_SOURCE,
      filter: ['==', '$type', 'LineString'],
      paint: {
        'line-color': '#0e6841',
        'line-width': 3,
        'line-dasharray': [1.2, 1],
      },
    });
    this.map.addLayer({
      id: DRAFT_VERTEX_LAYER,
      type: 'circle',
      source: DRAFT_SOURCE,
      filter: ['==', '$type', 'Point'],
      paint: {
        'circle-radius': 5,
        'circle-color': '#ffffff',
        'circle-stroke-color': '#0e6841',
        'circle-stroke-width': 2,
      },
    });
    this.map.addLayer({
      id: EDIT_VERTEX_LAYER,
      type: 'circle',
      source: VERTICES_SOURCE,
      paint: {
        'circle-radius': 6,
        'circle-color': '#ffffff',
        'circle-stroke-color': '#9c5c0a',
        'circle-stroke-width': 3,
      },
    });

    const source = this.getGeoJsonSource(FEATURES_SOURCE);
    if (source) {
      void source.setData(featureCollection).then(() => {
        this.map?.triggerRepaint();
      });
    }
  }

  private registerMapEvents(): void {
    if (!this.map) {
      return;
    }

    this.map.on('click', (event) => this.handleMapClick(event));
    this.map.on('dblclick', (event) => {
      if (this.mode() !== 'drawing') {
        return;
      }

      event.preventDefault();
      this.removeConsecutiveDuplicateDraftPosition();
      this.completeDrawing();
    });

    this.map.on('click', FEATURE_FILL_LAYER, (event) =>
      this.handleFeatureClick(event),
    );
    this.map.on('mouseenter', FEATURE_FILL_LAYER, () => {
      if (this.map && this.mode() === 'idle') {
        this.map.getCanvas().style.cursor = 'pointer';
      }
    });
    this.map.on('mouseleave', FEATURE_FILL_LAYER, () => {
      if (this.map && this.mode() === 'idle') {
        this.map.getCanvas().style.cursor = '';
      }
    });

    this.map.on('mousedown', EDIT_VERTEX_LAYER, (event) =>
      this.startVertexDrag(event),
    );
    this.map.on('mousemove', (event) => this.dragVertex(event));
    this.map.on('mouseup', () => this.finishVertexDrag());
  }

  private handleMapClick(event: MapMouseEvent): void {
    if (this.mode() !== 'drawing') {
      return;
    }

    this.draftPositions.push([event.lngLat.lng, event.lngLat.lat]);
    this.updateDraftSource();
  }

  private handleFeatureClick(event: MapLayerMouseEvent): void {
    if (this.mode() !== 'idle') {
      return;
    }

    const featureId = event.features?.[0]?.properties?.['id'];

    if (typeof featureId !== 'string') {
      return;
    }

    this.featureSelected.emit({ featureId });
  }

  private completeDrawing(): void {
    this.removeConsecutiveDuplicateDraftPosition();

    if (this.draftPositions.length < 3) {
      return;
    }

    const ring = this.draftPositions.map(
      ([longitude, latitude]) => [longitude, latitude] as MapPosition,
    );
    ring.push([...ring[0]] as MapPosition);

    const geometry: MapPolygonGeometry = {
      type: 'Polygon',
      coordinates: [ring],
    };

    this.updateDraftSource(geometry);
    this.finishInteraction();
    this.geometryCreated.emit({ geometry });
  }

  private startVertexDrag(event: MapLayerMouseEvent): void {
    if (this.mode() !== 'editing') {
      return;
    }

    const vertexIndex = event.features?.[0]?.properties?.['vertexIndex'];

    if (typeof vertexIndex !== 'number') {
      return;
    }

    event.preventDefault();
    this.activeVertexIndex = vertexIndex;
    this.map?.dragPan.disable();

    if (this.map) {
      this.map.getCanvas().style.cursor = 'grabbing';
    }
  }

  private dragVertex(event: MapMouseEvent): void {
    if (
      this.mode() !== 'editing' ||
      this.activeVertexIndex === null ||
      !this.selectedFeatureIdValue
    ) {
      return;
    }

    const featureIndex = this.mapFeatures.findIndex(
      (feature) => feature.id === this.selectedFeatureIdValue,
    );
    const feature = this.mapFeatures[featureIndex];
    const exteriorRing = feature?.geometry.coordinates[0];

    if (!feature || !exteriorRing) {
      return;
    }

    const nextPosition: MapPosition = [event.lngLat.lng, event.lngLat.lat];
    exteriorRing[this.activeVertexIndex] = nextPosition;

    if (this.activeVertexIndex === 0) {
      exteriorRing[exteriorRing.length - 1] = [...nextPosition];
    }

    this.mapFeatures[featureIndex] = {
      ...feature,
      geometry: this.cloneGeometry(feature.geometry),
    };
    this.updateFeatureSource();
    this.updateEditVertices();
  }

  private finishVertexDrag(): void {
    if (this.activeVertexIndex === null) {
      return;
    }

    this.activeVertexIndex = null;
    this.map?.dragPan.enable();

    if (this.map) {
      this.map.getCanvas().style.cursor = '';
    }

    const feature = this.getSelectedFeature();

    if (feature) {
      this.geometryEdited.emit({
        featureId: feature.id,
        geometry: this.cloneGeometry(feature.geometry),
      });
    }
  }

  private finishInteraction(): void {
    this.setMode('idle');
    this.map?.doubleClickZoom.enable();
    this.map?.dragPan.enable();
    this.updateEditVertices();
  }

  private setMode(mode: MapInteractionMode): void {
    this.mode.set(mode);
    this.modeChanged.emit(mode);

    if (this.map) {
      this.map.getCanvas().style.cursor =
        mode === 'drawing' ? 'crosshair' : '';
    }
  }

  private updateFeatureSource(): void {
    const source = this.getGeoJsonSource(FEATURES_SOURCE);

    if (!source) {
      return;
    }

    const featureCollection = this.createFeatureCollection();
    void source.setData(featureCollection).then(() => {
      this.map?.triggerRepaint();
    });
  }

  private createFeatureCollection(): FeatureCollection<Polygon> {
    const features: Feature<Polygon>[] = this.mapFeatures
      .filter((feature) => feature.visible !== false)
      .map((feature) => ({
        type: 'Feature',
        id: feature.id,
        properties: {
          ...feature.properties,
          id: feature.id,
        },
        geometry: this.cloneGeometry(feature.geometry),
      }));

    return {
      type: 'FeatureCollection',
      features,
    };
  }

  private updateDraftSource(completedGeometry?: MapPolygonGeometry): void {
    const source = this.getGeoJsonSource(DRAFT_SOURCE);

    if (!source) {
      return;
    }

    const features: Feature[] = [];

    if (completedGeometry) {
      features.push({
        type: 'Feature',
        properties: {},
        geometry: completedGeometry,
      });
    } else if (this.draftPositions.length > 0) {
      const points: Feature<Point>[] = this.draftPositions.map(
        (position, index) => ({
          type: 'Feature',
          properties: { vertexIndex: index },
          geometry: {
            type: 'Point',
            coordinates: position,
          },
        }),
      );
      features.push(...points);

      if (this.draftPositions.length >= 2) {
        const line: Feature<LineString> = {
          type: 'Feature',
          properties: {},
          geometry: {
            type: 'LineString',
            coordinates: this.draftPositions,
          },
        };
        features.push(line);
      }

      if (this.draftPositions.length >= 3) {
        const ring = [
          ...this.draftPositions,
          this.draftPositions[0],
        ] as MapPosition[];
        features.push({
          type: 'Feature',
          properties: {},
          geometry: {
            type: 'Polygon',
            coordinates: [ring],
          },
        });
      }
    }

    const featureCollection: FeatureCollection = {
      type: 'FeatureCollection',
      features,
    };

    void source.setData(featureCollection).then(() => {
      this.map?.triggerRepaint();
    });
  }

  private updateSelectionLayers(): void {
    if (
      !this.map?.getLayer(SELECTED_FILL_LAYER) ||
      !this.map.getLayer(SELECTED_OUTLINE_LAYER)
    ) {
      return;
    }

    const selectedId = this.selectedFeatureIdValue ?? '';
    const filter: [
      '==',
      ['get', string],
      string,
    ] = ['==', ['get', 'id'], selectedId];

    this.map.setFilter(SELECTED_FILL_LAYER, filter);
    this.map.setFilter(SELECTED_OUTLINE_LAYER, filter);
  }

  private updateEditVertices(): void {
    const source = this.getGeoJsonSource(VERTICES_SOURCE);

    if (!source) {
      return;
    }

    const selectedFeature = this.getSelectedFeature();
    const exteriorRing = selectedFeature?.geometry.coordinates[0] ?? [];
    const vertices: Feature<Point, GeoJsonProperties>[] =
      this.mode() === 'editing'
        ? exteriorRing.slice(0, -1).map((position, vertexIndex) => ({
            type: 'Feature',
            properties: { vertexIndex },
            geometry: {
              type: 'Point',
              coordinates: position,
            },
          }))
        : [];

    const featureCollection: FeatureCollection<Point> = {
      type: 'FeatureCollection',
      features: vertices,
    };

    void source.setData(featureCollection).then(() => {
      this.map?.triggerRepaint();
    });
  }

  private getSelectedFeature(): MapFeature | undefined {
    return this.mapFeatures.find(
      (feature) => feature.id === this.selectedFeatureIdValue,
    );
  }

  private getGeoJsonSource(id: string): GeoJSONSource | undefined {
    const source = this.map?.getSource(id);
    return source?.type === 'geojson'
      ? (source as GeoJSONSource)
      : undefined;
  }

  private fitToVisibleFeatures(): void {
    if (this.hasAutoFittedFeatures || !this.map) {
      return;
    }

    const positions = this.mapFeatures
      .filter((feature) => feature.visible !== false)
      .flatMap((feature) => feature.geometry.coordinates.flat());

    if (positions.length === 0) {
      return;
    }

    this.hasAutoFittedFeatures = true;
    const bounds = positions.reduce(
      (currentBounds, position) => currentBounds.extend(position),
      new LngLatBounds(positions[0], positions[0]),
    );

    this.map.fitBounds(bounds, {
      padding: 72,
      maxZoom: 16,
      duration: 0,
    });
  }

  private cloneGeometry(geometry: MapPolygonGeometry): MapPolygonGeometry {
    return {
      type: 'Polygon',
      coordinates: geometry.coordinates.map((ring) =>
        ring.map(
          ([longitude, latitude]) => [longitude, latitude] as MapPosition,
        ),
      ),
    };
  }

  private removeConsecutiveDuplicateDraftPosition(): void {
    const last = this.draftPositions[this.draftPositions.length - 1];
    const previous = this.draftPositions[this.draftPositions.length - 2];

    if (
      last &&
      previous &&
      last[0] === previous[0] &&
      last[1] === previous[1]
    ) {
      this.draftPositions.pop();
    }
  }
}
