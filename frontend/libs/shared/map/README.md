# Shared map

Reusable MapLibre-based GIS component for FormaGeo applications.

The library owns map behavior only. It has no project, site, HTTP, or API
dependencies.

## Capabilities

- Raster base maps with a layer selector
- GeoJSON Polygon display and visibility
- Polygon drawing, vertex editing, and removal requests
- Feature selection
- Geometry bounds fitting
- Zoom and metric scale controls
- ResizeObserver-based canvas resizing

## Inputs

- `features: MapFeature[]`
- `selectedFeatureId: string | null`
- `workerUrl: string`

The default worker URL is
`/assets/maplibre/maplibre-gl-worker.mjs`. Consuming applications must publish
both `maplibre-gl-worker.mjs` and `maplibre-gl-shared.mjs` from the MapLibre
distribution at that location, or provide a different `workerUrl`.

## Events

- `geometryCreated`
- `geometryEdited`
- `geometryDeleted`
- `featureSelected`
- `modeChanged`

## Imperative actions

Applications can obtain the component with `ViewChild(MapComponent)` and call:

- `startDrawing()` / `cancelDrawing()`
- `editSelected()` / `stopEditing()`
- `clearDraft()`
- `requestSelectedFeatureRemoval()`
- `fitToGeometry()`
- `resize()`

## Tests

Run `nx test map`.
