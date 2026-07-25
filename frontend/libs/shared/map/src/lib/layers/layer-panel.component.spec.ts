import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MapOverlay } from './layer.models';
import { LayerPanelComponent } from './layer-panel.component';

const OVERLAY: MapOverlay = {
  id: 'hazards',
  name: 'Flood susceptibility',
  description: 'Flood context',
  category: 'Hazards',
  geographicCoverage: 'Demonstration extent',
  coordinateSystem: 'EPSG:4326',
  geometryType: 'Polygon',
  featureNameProperty: 'name',
  style: {
    fillColor: '#0ea5e9',
    strokeColor: '#0369a1',
  },
  dataSource: {
    id: 'source',
    name: 'Test source',
    organization: 'Test organization',
    licenseName: 'CC0',
    licenseUrl: null,
    attribution: 'Test attribution',
    sourceUrl: null,
  },
  lastUpdatedAtUtc: '2026-07-01T00:00:00Z',
  deliveryMethod: 'GeoJson',
  dataUrl: '/layers/flood.geojson',
  sourceLayer: null,
  minimumZoom: null,
  maximumZoom: null,
  legend: [
    {
      id: 'legend',
      label: 'Moderate susceptibility',
      fillColor: '#0ea5e9',
      strokeColor: '#0369a1',
      symbol: null,
      sortOrder: 0,
    },
  ],
  visible: false,
  opacity: 0.8,
  sortOrder: 0,
  filter: null,
};

describe('LayerPanelComponent', () => {
  let fixture: ComponentFixture<LayerPanelComponent>;
  let component: LayerPanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LayerPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(LayerPanelComponent);
    component = fixture.componentInstance;
    component.overlays = [OVERLAY];
    component.baseMaps = [
      { id: 'light', label: 'Light' },
      { id: 'streets', label: 'Streets' },
    ];
    fixture.detectChanges();
  });

  it('keeps base maps in their own group', () => {
    (
      fixture.nativeElement.querySelector(
        '.fg-layers-toggle',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    const panelText = fixture.nativeElement.textContent;

    expect(panelText).toContain('Base maps');
    expect(panelText).toContain('Light');
    expect(panelText).toContain('Hazards');
  });

  it('emits the complete preference when visibility changes', () => {
    const changes = vi.fn();
    component.overlayChanged.subscribe(changes);

    (
      fixture.nativeElement.querySelector(
        '.fg-layers-toggle',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    const categoryButtons = Array.from(
      fixture.nativeElement.querySelectorAll(
        '.fg-layer-group__heading',
      ),
    ) as HTMLButtonElement[];
    categoryButtons
      .find((button) => button.textContent?.includes('Hazards'))
      ?.click();
    fixture.detectChanges();

    (
      fixture.nativeElement.querySelector(
        '.fg-overlay-row__visibility',
      ) as HTMLButtonElement
    ).click();

    expect(changes).toHaveBeenCalledWith({
      layerId: 'hazards',
      visible: true,
      opacity: 0.8,
      sortOrder: 0,
      filter: null,
    });
  });
});
