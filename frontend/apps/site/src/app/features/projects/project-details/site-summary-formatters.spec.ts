import { describe, expect, it } from 'vitest';
import {
  formatArea,
  formatCentroid,
  formatPerimeter,
} from './site-summary-formatters';

describe('site summary formatters', () => {
  it('uses square metres for small areas', () => {
    expect(formatArea(950.4)).toBe('950.4 m²');
  });

  it('uses hectares for large areas', () => {
    expect(formatArea(18_400)).toBe('1.84 ha');
  });

  it('uses kilometres for long perimeters', () => {
    expect(formatPerimeter(1_250)).toBe('1.25 km');
  });

  it('formats centroid as longitude then latitude', () => {
    expect(
      formatCentroid({
        longitude: 121.031,
        latitude: 14.6507,
      }),
    ).toBe('121.0310, 14.6507');
  });
});
