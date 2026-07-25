import { SiteCoordinate } from '@frontend/models';

const squareMetreFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 1,
});
const hectareFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const metreFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 0,
});
const kilometreFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function formatArea(areaSquareMetres: number | null): string {
  if (areaSquareMetres === null) {
    return 'Unavailable';
  }

  if (areaSquareMetres >= 10_000) {
    return `${hectareFormatter.format(areaSquareMetres / 10_000)} ha`;
  }

  return `${squareMetreFormatter.format(areaSquareMetres)} m²`;
}

export function formatPerimeter(perimeterMetres: number | null): string {
  if (perimeterMetres === null) {
    return 'Unavailable';
  }

  if (perimeterMetres >= 1_000) {
    return `${kilometreFormatter.format(perimeterMetres / 1_000)} km`;
  }

  return `${metreFormatter.format(perimeterMetres)} m`;
}

export function formatCentroid(centroid: SiteCoordinate | null): string {
  return centroid
    ? `${centroid.longitude.toFixed(4)}, ${centroid.latitude.toFixed(4)}`
    : 'Unavailable';
}

export function formatCoordinate(value: number): string {
  return value.toFixed(6);
}
