import { describe, expect, it } from 'vitest';
import { parseGeoJsonImport } from './geojson-import.parser';

describe('parseGeoJsonImport', () => {
  it('parses named Polygon features', () => {
    const preview = parseGeoJsonImport(
      JSON.stringify({
        type: 'FeatureCollection',
        features: [
          {
            type: 'Feature',
            properties: { name: 'North parcel' },
            geometry: {
              type: 'Polygon',
              coordinates: [
                [
                  [121, 14],
                  [121.1, 14],
                  [121.1, 14.1],
                  [121, 14],
                ],
              ],
            },
          },
        ],
      }),
      'sites.geojson',
    );

    expect(preview.featureCount).toBe(1);
    expect(preview.invalidFeatureCount).toBe(0);
    expect(preview.features[0].name).toBe('North parcel');
  });

  it('transforms declared Web Mercator coordinates for preview', () => {
    const preview = parseGeoJsonImport(
      JSON.stringify({
        type: 'Polygon',
        crs: {
          type: 'name',
          properties: { name: 'EPSG:3857' },
        },
        coordinates: [
          [
            [0, 0],
            [111319.490793, 0],
            [111319.490793, 111325.142866],
            [0, 0],
          ],
        ],
      }),
      'mercator.geojson',
    );

    const coordinates =
      preview.features[0].polygons[0].coordinates[0];

    expect(coordinates[1][0]).toBeCloseTo(1, 5);
    expect(coordinates[2][1]).toBeCloseTo(1, 5);
    expect(preview.detectedCoordinateSystem).toContain('EPSG:3857');
  });

  it('reports unsupported geometry without discarding the review', () => {
    const preview = parseGeoJsonImport(
      JSON.stringify({
        type: 'Feature',
        properties: {},
        geometry: {
          type: 'Point',
          coordinates: [121, 14],
        },
      }),
      'point.geojson',
    );

    expect(preview.invalidFeatureCount).toBe(1);
    expect(preview.features[0].issues[0]).toContain('not supported');
  });
});
