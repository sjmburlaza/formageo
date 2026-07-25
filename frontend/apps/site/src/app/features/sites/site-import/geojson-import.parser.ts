import { GeoJsonPolygon, GeoJsonPosition } from '@frontend/models';

const WEB_MERCATOR_RADIUS = 6_378_137;

interface GeoJsonRecord {
  [key: string]: unknown;
}

export interface GeoJsonImportPreviewFeature {
  index: number;
  name: string;
  polygons: GeoJsonPolygon[];
  issues: string[];
  warnings: string[];
}

export interface GeoJsonImportPreview {
  fileName: string;
  content: string;
  detectedCoordinateSystem: string;
  featureCount: number;
  invalidFeatureCount: number;
  features: GeoJsonImportPreviewFeature[];
}

export class GeoJsonPreviewError extends Error {}

export function parseGeoJsonImport(
  content: string,
  fileName: string,
): GeoJsonImportPreview {
  let root: unknown;

  try {
    root = JSON.parse(content);
  } catch {
    throw new GeoJsonPreviewError(
      'This file is not valid JSON. Check for missing commas or brackets.',
    );
  }

  if (!isRecord(root)) {
    throw new GeoJsonPreviewError('The GeoJSON root must be an object.');
  }

  const sourceSrid = detectSrid(root);
  const fallbackName =
    fileName.replace(/\.(geo)?json$/i, '').trim() || 'Imported site';
  const featureRecords = collectFeatureRecords(root);
  const features = featureRecords.map((feature, index) =>
    parseFeature(feature, index, fallbackName, sourceSrid),
  );

  return {
    fileName,
    content,
    detectedCoordinateSystem:
      sourceSrid === 3857
        ? 'Web Mercator (EPSG:3857) · will convert to WGS 84'
        : 'WGS 84 (EPSG:4326)',
    featureCount: features.length,
    invalidFeatureCount: features.filter(
      (feature) => feature.issues.length > 0,
    ).length,
    features,
  };
}

function collectFeatureRecords(root: GeoJsonRecord): GeoJsonRecord[] {
  const type = stringProperty(root, 'type');

  if (type === 'FeatureCollection') {
    if (!Array.isArray(root['features'])) {
      throw new GeoJsonPreviewError(
        'A FeatureCollection must contain a features array.',
      );
    }

    return root['features'].map((feature) => {
      if (!isRecord(feature)) {
        return { type: 'InvalidFeature' };
      }

      return feature;
    });
  }

  return [root];
}

function parseFeature(
  record: GeoJsonRecord,
  index: number,
  fallbackName: string,
  sourceSrid: number,
): GeoJsonImportPreviewFeature {
  const isFeature = record['type'] === 'Feature';
  const geometry = isFeature ? record['geometry'] : record;
  const name = isFeature
    ? readFeatureName(record, `${fallbackName} ${index + 1}`)
    : featureDefaultName(fallbackName, index);
  const issues: string[] = [];
  const warnings: string[] = [];
  const polygons: GeoJsonPolygon[] = [];

  if (!isRecord(geometry)) {
    issues.push('The feature does not contain a geometry.');
    return { index, name, polygons, issues, warnings };
  }

  const geometryType = geometry['type'];
  const coordinates = geometry['coordinates'];

  if (geometryType !== 'Polygon' && geometryType !== 'MultiPolygon') {
    issues.push(
      `Geometry type '${String(geometryType ?? 'unknown')}' is not supported. Use Polygon or MultiPolygon.`,
    );
    return { index, name, polygons, issues, warnings };
  }

  const polygonCoordinates =
    geometryType === 'Polygon' ? [coordinates] : coordinates;

  if (!Array.isArray(polygonCoordinates) || polygonCoordinates.length === 0) {
    issues.push('The geometry does not contain polygon coordinates.');
    return { index, name, polygons, issues, warnings };
  }

  for (const candidate of polygonCoordinates) {
    const parsed = parsePolygonCoordinates(candidate, sourceSrid);

    if (parsed.issue) {
      issues.push(parsed.issue);
    } else if (parsed.polygon) {
      polygons.push(parsed.polygon);

      if (parsed.warning) {
        warnings.push(parsed.warning);
      }
    }
  }

  if (polygons.length === 0 && issues.length === 0) {
    issues.push('The geometry does not contain a valid polygon.');
  }

  return { index, name, polygons, issues, warnings };
}

function parsePolygonCoordinates(
  value: unknown,
  sourceSrid: number,
): {
  polygon?: GeoJsonPolygon;
  issue?: string;
  warning?: string;
} {
  if (!Array.isArray(value) || value.length === 0) {
    return { issue: 'A Polygon must contain at least one ring.' };
  }

  const rings: GeoJsonPosition[][] = [];

  for (const ringValue of value) {
    if (!Array.isArray(ringValue) || ringValue.length < 4) {
      return {
        issue: 'Every polygon ring must contain at least four positions.',
      };
    }

    const ring: GeoJsonPosition[] = [];

    for (const positionValue of ringValue) {
      const position = parsePosition(positionValue, sourceSrid);

      if (!position) {
        return {
          issue:
            'Every position must contain finite [longitude, latitude] numbers in range.',
        };
      }

      ring.push(position);
    }

    if (!samePosition(ring[0], ring[ring.length - 1])) {
      return {
        issue:
          'Every polygon ring must be closed; its first and last positions must match.',
      };
    }

    rings.push(ring);
  }

  const hasSelfIntersection = rings.some((ring) => ringSelfIntersects(ring));

  return {
    polygon: {
      type: 'Polygon',
      coordinates: rings,
    },
    warning: hasSelfIntersection
      ? 'This polygon self-intersects. The importer will repair it when safe.'
      : undefined,
  };
}

function parsePosition(
  value: unknown,
  sourceSrid: number,
): GeoJsonPosition | null {
  if (
    !Array.isArray(value) ||
    value.length !== 2 ||
    typeof value[0] !== 'number' ||
    typeof value[1] !== 'number' ||
    !Number.isFinite(value[0]) ||
    !Number.isFinite(value[1])
  ) {
    return null;
  }

  const transformed =
    sourceSrid === 3857
      ? webMercatorToWgs84(value[0], value[1])
      : ([value[0], value[1]] as GeoJsonPosition);

  return transformed[0] >= -180 &&
    transformed[0] <= 180 &&
    transformed[1] >= -90 &&
    transformed[1] <= 90
    ? transformed
    : null;
}

function webMercatorToWgs84(
  x: number,
  y: number,
): GeoJsonPosition {
  const longitude = (x / WEB_MERCATOR_RADIUS) * (180 / Math.PI);
  const latitude =
    (2 * Math.atan(Math.exp(y / WEB_MERCATOR_RADIUS)) - Math.PI / 2) *
    (180 / Math.PI);

  return [longitude, latitude];
}

function detectSrid(root: GeoJsonRecord): number {
  if (root['crs'] === undefined || root['crs'] === null) {
    return 4326;
  }

  const crs = root['crs'];
  const properties = isRecord(crs) ? crs['properties'] : undefined;
  const name =
    isRecord(properties) && typeof properties['name'] === 'string'
      ? properties['name'].toUpperCase()
      : '';

  if (name.includes('3857') || name.includes('900913')) {
    return 3857;
  }

  if (name.includes('4326') || name.includes('CRS84')) {
    return 4326;
  }

  throw new GeoJsonPreviewError(
    name
      ? `Coordinate system '${name}' is not supported. Use EPSG:4326 or EPSG:3857.`
      : 'The GeoJSON coordinate system could not be detected.',
  );
}

function readFeatureName(
  feature: GeoJsonRecord,
  fallback: string,
): string {
  const properties = feature['properties'];

  if (!isRecord(properties)) {
    return fallback;
  }

  for (const key of ['name', 'title', 'id']) {
    const value = properties[key];

    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }

  return fallback;
}

function featureDefaultName(
  fallbackName: string,
  index: number,
): string {
  return index === 0 ? fallbackName : `${fallbackName} ${index + 1}`;
}

function stringProperty(
  record: GeoJsonRecord,
  property: string,
): string {
  const value = record[property];

  if (typeof value !== 'string' || !value.trim()) {
    throw new GeoJsonPreviewError(
      `GeoJSON '${property}' must be a string.`,
    );
  }

  return value;
}

function ringSelfIntersects(ring: GeoJsonPosition[]): boolean {
  for (let first = 0; first < ring.length - 1; first++) {
    for (let second = first + 1; second < ring.length - 1; second++) {
      const adjacent =
        Math.abs(first - second) <= 1 ||
        (first === 0 && second === ring.length - 2);

      if (
        !adjacent &&
        segmentsIntersect(
          ring[first],
          ring[first + 1],
          ring[second],
          ring[second + 1],
        )
      ) {
        return true;
      }
    }
  }

  return false;
}

function segmentsIntersect(
  a: GeoJsonPosition,
  b: GeoJsonPosition,
  c: GeoJsonPosition,
  d: GeoJsonPosition,
): boolean {
  const orientation = (
    first: GeoJsonPosition,
    second: GeoJsonPosition,
    third: GeoJsonPosition,
  ) =>
    (second[1] - first[1]) * (third[0] - second[0]) -
    (second[0] - first[0]) * (third[1] - second[1]);

  const first = orientation(a, b, c);
  const second = orientation(a, b, d);
  const third = orientation(c, d, a);
  const fourth = orientation(c, d, b);

  return first * second < 0 && third * fourth < 0;
}

function samePosition(
  first: GeoJsonPosition,
  second: GeoJsonPosition,
): boolean {
  return first[0] === second[0] && first[1] === second[1];
}

function isRecord(value: unknown): value is GeoJsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
