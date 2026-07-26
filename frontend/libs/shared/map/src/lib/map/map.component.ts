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
  GeoJSON,
  GeoJsonProperties,
  LineString,
  Point,
  Polygon,
} from 'geojson';
import {
  FilterSpecification,
  GeoJSONSource,
  LngLatBounds,
  Map as MapLibreMap,
  MapLayerMouseEvent,
  MapMouseEvent,
  Marker,
  NavigationControl,
  ScaleControl,
  setWorkerUrl,
  StyleSpecification,
} from 'maplibre-gl';
import {
  LucideExpand,
  LucidePencil,
  LucidePenTool,
  LucideTrash2,
  LucideUndo2,
  LucideX,
} from '@lucide/angular';
import { LayerPanelComponent } from '../layers/layer-panel.component';
import {
  BaseMapDefinition,
  MapIdentifiedFeature,
  MapOverlay,
  MapOverlayStateChange,
} from '../layers/layer.models';

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
  imports: [
    LucideExpand,
    LucidePencil,
    LucidePenTool,
    LucideTrash2,
    LucideUndo2,
    LucideX,
    LayerPanelComponent,
  ],
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
  private highlightedMarker?: Marker;
  private mapOverlays: MapOverlay[] = [];
  private readonly registeredOverlayLayerIds = new Set<string>();

  protected readonly mode = signal<MapInteractionMode>('idle');
  protected readonly selectedFeatureIdSignal = signal<string | null>(null);
  protected readonly activeBaseMap = signal('light');
  protected readonly mapReady = signal(false);
  protected readonly overlaySignal = signal<MapOverlay[]>([]);
  protected readonly identifiedFeature = signal<MapIdentifiedFeature | null>(
    null,
  );
  protected readonly baseMaps: BaseMapDefinition[] = [
    { id: 'light', label: 'Light' },
    { id: 'streets', label: 'Streets' },
    { id: 'satellite', label: 'Satellite' },
  ];

  readonly geometryCreated = output<MapGeometryEvent>();
  readonly geometryEdited = output<MapGeometryEvent>();
  readonly geometryDeleted = output<MapFeatureSelectionEvent>();
  readonly featureSelected = output<MapFeatureSelectionEvent>();
  readonly modeChanged = output<MapInteractionMode>();
  readonly overlayStateChanged = output<MapOverlayStateChange>();
  readonly overlayFeatureSelected = output<MapIdentifiedFeature>();
  readonly baseMapChanged = output<string>();

  @Input() workerUrl = '/assets/maplibre/maplibre-gl-worker.mjs';
  @Input() interactionTools = true;

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

  @Input()
  set overlays(value: MapOverlay[] | null | undefined) {
    this.mapOverlays = (value ?? []).map((overlay) => ({
      ...overlay,
      legend: [...overlay.legend],
      style: { ...overlay.style },
      dataSource: { ...overlay.dataSource },
    }));
    this.overlaySignal.set(this.mapOverlays);
    this.synchronizeOverlayLayers();
  }

  ngAfterViewInit(): void {
    this.initializeMap();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.highlightedMarker?.remove();
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

  highlightPosition(position: MapPosition): void {
    if (
      !this.map ||
      !Number.isFinite(position[0]) ||
      !Number.isFinite(position[1])
    ) {
      return;
    }

    this.clearHighlightedPosition();
    this.highlightedMarker = new Marker({
      color: '#0f766e',
    })
      .setLngLat(position)
      .addTo(this.map);
    this.highlightedMarker
      .getElement()
      .setAttribute('aria-label', 'Site centroid');

    this.map.easeTo({
      center: position,
      zoom: Math.max(this.map.getZoom(), 15),
      duration: 650,
    });
  }

  clearHighlightedPosition(): void {
    this.highlightedMarker?.remove();
    this.highlightedMarker = undefined;
  }

  protected changeBaseMap(baseMapId: string): void {
    this.activeBaseMap.set(baseMapId);

    for (const baseMap of this.baseMaps) {
      this.map?.setLayoutProperty(
        `fg-basemap-${baseMap.id}`,
        'visibility',
        baseMap.id === baseMapId ? 'visible' : 'none',
      );
    }

    this.baseMapChanged.emit(baseMapId);
  }

  protected handleOverlayStateChange(change: MapOverlayStateChange): void {
    this.mapOverlays = this.mapOverlays.map((overlay) =>
      overlay.id === change.layerId
        ? {
            ...overlay,
            visible: change.visible,
            opacity: change.opacity,
            sortOrder: change.sortOrder,
            filter: change.filter,
          }
        : overlay,
    );
    this.overlaySignal.set(this.mapOverlays);
    this.synchronizeOverlayLayers();
    this.overlayStateChanged.emit(change);

    if (
      this.identifiedFeature()?.layerId === change.layerId &&
      !change.visible
    ) {
      this.identifiedFeature.set(null);
    }
  }

  protected closeIdentifiedFeature(): void {
    this.identifiedFeature.set(null);
  }

  protected selectedFeatureEditable(): boolean {
    return this.getSelectedFeature()?.properties?.['status'] !== 'Archived';
  }

  protected undoLastPoint(): void {
    if (this.mode() !== 'drawing' || this.draftPositions.length === 0) {
      return;
    }

    this.draftPositions.pop();
    this.updateDraftSource();
  }

  protected canUndoDrawing(): boolean {
    return this.draftPositions.length > 0;
  }

  protected fitSelectedFeature(): void {
    const feature = this.getSelectedFeature();

    if (feature) {
      this.fitToGeometry(feature.geometry);
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
      this.synchronizeOverlayLayers();
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
          layout: { visibility: 'none' },
        },
        {
          id: 'fg-basemap-light',
          type: 'raster',
          source: 'fg-basemap-light-source',
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
        'fill-color': [
          'case',
          ['==', ['get', 'comparisonSelected'], true],
          '#6366f1',
          ['==', ['get', 'status'], 'Archived'],
          '#94a3b8',
          '#10b981',
        ],
        'fill-opacity': [
          'case',
          ['==', ['get', 'comparisonSelected'], true],
          0.42,
          ['==', ['get', 'status'], 'Archived'],
          0.2,
          0.38,
        ],
      },
    });
    this.map.addLayer({
      id: FEATURE_OUTLINE_LAYER,
      type: 'line',
      source: FEATURES_SOURCE,
      paint: {
        'line-color': [
          'case',
          ['==', ['get', 'comparisonSelected'], true],
          '#4338ca',
          ['==', ['get', 'status'], 'Archived'],
          '#64748b',
          '#047857',
        ],
        'line-width': [
          'case',
          ['==', ['get', 'comparisonSelected'], true],
          2.5,
          2,
        ],
      },
    });
    this.map.addLayer({
      id: SELECTED_FILL_LAYER,
      type: 'fill',
      source: FEATURES_SOURCE,
      filter: ['==', ['get', 'id'], ''],
      paint: {
        'fill-color': '#f59e0b',
        'fill-opacity': 0.52,
      },
    });
    this.map.addLayer({
      id: SELECTED_OUTLINE_LAYER,
      type: 'line',
      source: FEATURES_SOURCE,
      filter: ['==', ['get', 'id'], ''],
      paint: {
        'line-color': '#b45309',
        'line-width': 3,
      },
    });
    this.map.addLayer({
      id: DRAFT_FILL_LAYER,
      type: 'fill',
      source: DRAFT_SOURCE,
      filter: ['==', '$type', 'Polygon'],
      paint: {
        'fill-color': '#34d399',
        'fill-opacity': 0.22,
      },
    });
    this.map.addLayer({
      id: DRAFT_LINE_LAYER,
      type: 'line',
      source: DRAFT_SOURCE,
      filter: ['==', '$type', 'LineString'],
      paint: {
        'line-color': '#059669',
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
        'circle-stroke-color': '#059669',
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
        'circle-stroke-color': '#b45309',
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

  private synchronizeOverlayLayers(): void {
    if (!this.map?.getLayer(FEATURE_FILL_LAYER)) {
      return;
    }

    for (const overlay of this.mapOverlays) {
      if (overlay.visible) {
        this.ensureOverlayLayer(overlay);
        this.updateOverlayAppearance(overlay);
      } else {
        this.removeOverlayLayer(overlay);
      }
    }

    const orderedOverlays = this.mapOverlays
      .filter((overlay) => overlay.visible)
      .sort((left, right) => right.sortOrder - left.sortOrder);

    for (const overlay of orderedOverlays) {
      for (const layerId of this.getOverlayLayerIds(overlay)) {
        if (this.map.getLayer(layerId)) {
          this.map.moveLayer(layerId, FEATURE_FILL_LAYER);
        }
      }
    }
  }

  private ensureOverlayLayer(overlay: MapOverlay): void {
    if (!this.map) {
      return;
    }

    const sourceId = this.getOverlaySourceId(overlay.id);

    if (!this.map.getSource(sourceId)) {
      if (overlay.deliveryMethod === 'GeoJson') {
        this.map.addSource(sourceId, {
          type: 'geojson',
          data: (overlay.data ?? overlay.dataUrl) as string | GeoJSON,
          attribution: overlay.dataSource.attribution,
        });
      } else if (overlay.deliveryMethod === 'VectorTiles') {
        this.map.addSource(sourceId, {
          type: 'vector',
          tiles: [overlay.dataUrl],
          minzoom: overlay.minimumZoom ?? undefined,
          maxzoom: overlay.maximumZoom ?? undefined,
          attribution: overlay.dataSource.attribution,
        });
      } else {
        this.map.addSource(sourceId, {
          type: 'raster',
          tiles: [overlay.dataUrl],
          tileSize: 256,
          minzoom: overlay.minimumZoom ?? undefined,
          maxzoom: overlay.maximumZoom ?? undefined,
          attribution: overlay.dataSource.attribution,
        });
      }
    }

    const layerIds = this.getOverlayLayerIds(overlay);
    const sourceLayer =
      overlay.deliveryMethod === 'VectorTiles'
        ? (overlay.sourceLayer ?? undefined)
        : undefined;

    if (overlay.geometryType === 'Polygon') {
      if (!this.map.getLayer(layerIds[0])) {
        this.map.addLayer(
          {
            id: layerIds[0],
            type: 'fill',
            source: sourceId,
            'source-layer': sourceLayer,
            paint: {
              'fill-color': overlay.style.fillColor ?? '#64748b',
            },
          },
          FEATURE_FILL_LAYER,
        );
      }

      if (!this.map.getLayer(layerIds[1])) {
        this.map.addLayer(
          {
            id: layerIds[1],
            type: 'line',
            source: sourceId,
            'source-layer': sourceLayer,
            paint: {
              'line-color':
                overlay.style.strokeColor ??
                overlay.style.fillColor ??
                '#334155',
              'line-width': overlay.style.strokeWidth ?? 1.5,
            },
          },
          FEATURE_FILL_LAYER,
        );
      }
    } else if (overlay.geometryType === 'LineString') {
      if (!this.map.getLayer(layerIds[0])) {
        this.map.addLayer(
          {
            id: layerIds[0],
            type: 'line',
            source: sourceId,
            'source-layer': sourceLayer,
            paint: {
              'line-color':
                overlay.style.lineColor ??
                overlay.style.strokeColor ??
                '#334155',
              'line-width':
                overlay.style.lineWidth ?? overlay.style.strokeWidth ?? 2,
            },
          },
          FEATURE_FILL_LAYER,
        );
      }
    } else if (overlay.geometryType === 'Point') {
      if (!this.map.getLayer(layerIds[0])) {
        this.map.addLayer(
          {
            id: layerIds[0],
            type: 'circle',
            source: sourceId,
            'source-layer': sourceLayer,
            paint: {
              'circle-color': overlay.style.circleColor ?? '#7c3aed',
              'circle-radius': overlay.style.circleRadius ?? 6,
              'circle-stroke-color': overlay.style.strokeColor ?? '#ffffff',
              'circle-stroke-width': overlay.style.strokeWidth ?? 1.5,
            },
          },
          FEATURE_FILL_LAYER,
        );
      }
    } else if (!this.map.getLayer(layerIds[0])) {
      this.map.addLayer(
        {
          id: layerIds[0],
          type: 'raster',
          source: sourceId,
        },
        FEATURE_FILL_LAYER,
      );
    }

    const interactiveLayerId = layerIds[0];

    if (
      overlay.geometryType !== 'Raster' &&
      !this.registeredOverlayLayerIds.has(interactiveLayerId)
    ) {
      this.registeredOverlayLayerIds.add(interactiveLayerId);
      this.map.on('click', interactiveLayerId, (event) =>
        this.handleOverlayFeatureClick(overlay.id, event),
      );
      this.map.on('mouseenter', interactiveLayerId, () => {
        if (this.map && this.mode() === 'idle') {
          this.map.getCanvas().style.cursor = 'pointer';
        }
      });
      this.map.on('mouseleave', interactiveLayerId, () => {
        if (this.map && this.mode() === 'idle') {
          this.map.getCanvas().style.cursor = '';
        }
      });
    }
  }

  private removeOverlayLayer(overlay: MapOverlay): void {
    if (!this.map) {
      return;
    }

    for (const layerId of [...this.getOverlayLayerIds(overlay)].reverse()) {
      if (this.map.getLayer(layerId)) {
        this.map.removeLayer(layerId);
      }
    }

    const sourceId = this.getOverlaySourceId(overlay.id);

    if (this.map.getSource(sourceId)) {
      this.map.removeSource(sourceId);
    }
  }

  private updateOverlayAppearance(overlay: MapOverlay): void {
    if (!this.map) {
      return;
    }

    const [primaryLayerId, outlineLayerId] = this.getOverlayLayerIds(overlay);
    const opacity = Math.min(1, Math.max(0, overlay.opacity));

    if (overlay.geometryType === 'Polygon') {
      if (this.map.getLayer(primaryLayerId)) {
        this.map.setPaintProperty(
          primaryLayerId,
          'fill-opacity',
          (overlay.style.fillOpacity ?? 0.35) * opacity,
        );
      }

      if (outlineLayerId && this.map.getLayer(outlineLayerId)) {
        this.map.setPaintProperty(outlineLayerId, 'line-opacity', opacity);
      }
    } else if (overlay.geometryType === 'LineString') {
      this.map.setPaintProperty(primaryLayerId, 'line-opacity', opacity);
    } else if (overlay.geometryType === 'Point') {
      this.map.setPaintProperty(primaryLayerId, 'circle-opacity', opacity);
      this.map.setPaintProperty(
        primaryLayerId,
        'circle-stroke-opacity',
        opacity,
      );
    } else {
      this.map.setPaintProperty(primaryLayerId, 'raster-opacity', opacity);
    }

    if (overlay.geometryType !== 'Raster') {
      const filter = this.createOverlayFilter(overlay);

      for (const layerId of this.getOverlayLayerIds(overlay)) {
        if (this.map.getLayer(layerId)) {
          this.map.setFilter(layerId, filter);
        }
      }
    }
  }

  private createOverlayFilter(overlay: MapOverlay): FilterSpecification | null {
    if (!overlay.filter) {
      return null;
    }

    return [
      '>=',
      [
        'index-of',
        overlay.filter.toLocaleLowerCase(),
        ['downcase', ['to-string', ['get', overlay.featureNameProperty]]],
      ],
      0,
    ] as FilterSpecification;
  }

  private handleOverlayFeatureClick(
    overlayId: string,
    event: MapLayerMouseEvent,
  ): void {
    if (this.mode() !== 'idle') {
      return;
    }

    const overlay = this.mapOverlays.find(
      (candidate) => candidate.id === overlayId,
    );
    const properties = event.features?.[0]?.properties;

    if (!overlay || !properties) {
      return;
    }

    const featureNameValue =
      properties[overlay.featureNameProperty] ??
      properties['name'] ??
      'Unnamed feature';
    const feature: MapIdentifiedFeature = {
      layerId: overlay.id,
      layerName: overlay.name,
      featureName: String(featureNameValue),
      attributes: Object.entries(properties)
        .filter(
          ([key, value]) =>
            value !== null &&
            value !== undefined &&
            key !== overlay.featureNameProperty &&
            key !== 'name' &&
            key !== 'id',
        )
        .slice(0, 8)
        .map(([key, value]) => ({
          label: this.formatPropertyLabel(key),
          value: String(value),
        })),
      source: overlay.dataSource,
      lastUpdatedAtUtc: overlay.lastUpdatedAtUtc,
    };

    this.identifiedFeature.set(feature);
    this.overlayFeatureSelected.emit(feature);
  }

  private getOverlaySourceId(layerId: string): string {
    return `fg-overlay-source-${layerId}`;
  }

  private getOverlayLayerIds(overlay: MapOverlay): string[] {
    const baseId = `fg-overlay-${overlay.id}`;

    return overlay.geometryType === 'Polygon'
      ? [`${baseId}-fill`, `${baseId}-outline`]
      : [baseId];
  }

  private formatPropertyLabel(key: string): string {
    return key
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .replace(/^./, (character) => character.toLocaleUpperCase());
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

  protected completeDrawing(): void {
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
      this.map.getCanvas().style.cursor = mode === 'drawing' ? 'crosshair' : '';
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
    const filter: ['==', ['get', string], string] = [
      '==',
      ['get', 'id'],
      selectedId,
    ];

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
    return source?.type === 'geojson' ? (source as GeoJSONSource) : undefined;
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
