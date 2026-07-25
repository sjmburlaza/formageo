import {
  ChangeDetectionStrategy,
  Component,
  Input,
  output,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import {
  LucideArrowDown,
  LucideArrowUp,
  LucideChevronDown,
  LucideChevronRight,
  LucideEye,
  LucideEyeOff,
  LucideFilter,
  LucideInfo,
  LucideLayers3,
  LucideList,
  LucideX,
} from '@lucide/angular';
import {
  BaseMapDefinition,
  MapOverlay,
  MapOverlayStateChange,
} from './layer.models';
import { LayerCategory } from '@frontend/models';

const CATEGORY_ORDER: LayerCategory[] = [
  'Boundaries',
  'Planning',
  'Hazards',
  'Environment',
  'Transport',
  'Facilities',
];

@Component({
  selector: 'fg-layer-panel',
  standalone: true,
  imports: [
    LucideArrowDown,
    LucideArrowUp,
    LucideChevronDown,
    LucideChevronRight,
    LucideEye,
    LucideEyeOff,
    LucideFilter,
    LucideInfo,
    LucideLayers3,
    LucideList,
    LucideX,
  ],
  templateUrl: './layer-panel.component.html',
  styleUrl: './layer-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class LayerPanelComponent {
  @Input() overlays: MapOverlay[] = [];
  @Input() baseMaps: BaseMapDefinition[] = [];
  @Input() activeBaseMap = 'light';

  readonly baseMapChanged = output<string>();
  readonly overlayChanged = output<MapOverlayStateChange>();

  protected readonly open = signal(false);
  protected readonly expandedCategories =
    signal<ReadonlySet<string>>(new Set(['Base maps']));
  protected readonly legendLayerId = signal<string | null>(null);
  protected readonly infoLayerId = signal<string | null>(null);
  protected readonly filterLayerId = signal<string | null>(null);
  protected readonly categoryOrder = CATEGORY_ORDER;

  protected toggleOpen(): void {
    this.open.update((open) => !open);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected overlaysForCategory(category: LayerCategory): MapOverlay[] {
    return this.overlays
      .filter((overlay) => overlay.category === category)
      .sort((left, right) => left.sortOrder - right.sortOrder);
  }

  protected categoryVisible(category: LayerCategory): boolean {
    return this.overlaysForCategory(category).length > 0;
  }

  protected categoryExpanded(category: string): boolean {
    return this.expandedCategories().has(category);
  }

  protected toggleCategory(category: string): void {
    this.expandedCategories.update((categories) => {
      const nextCategories = new Set(categories);

      if (nextCategories.has(category)) {
        nextCategories.delete(category);
      } else {
        nextCategories.add(category);
      }

      return nextCategories;
    });
  }

  protected selectBaseMap(baseMapId: string): void {
    this.baseMapChanged.emit(baseMapId);
  }

  protected toggleVisibility(overlay: MapOverlay): void {
    this.emitChange(overlay, {
      visible: !overlay.visible,
    });
  }

  protected updateOpacity(overlay: MapOverlay, event: Event): void {
    const opacity =
      Number((event.target as HTMLInputElement).value) / 100;
    this.emitChange(overlay, { opacity });
  }

  protected updateFilter(overlay: MapOverlay, event: Event): void {
    const filter =
      (event.target as HTMLInputElement).value.trim() || null;
    this.emitChange(overlay, { filter });
  }

  protected moveOverlay(overlay: MapOverlay, direction: -1 | 1): void {
    const ordered = [...this.overlays].sort(
      (left, right) => left.sortOrder - right.sortOrder,
    );
    const index = ordered.findIndex(
      (candidate) => candidate.id === overlay.id,
    );
    const swapIndex = index + direction;

    if (index < 0 || swapIndex < 0 || swapIndex >= ordered.length) {
      return;
    }

    const other = ordered[swapIndex];
    this.emitChange(overlay, { sortOrder: other.sortOrder });
    this.emitChange(other, { sortOrder: overlay.sortOrder });
  }

  protected toggleLegend(layerId: string): void {
    this.legendLayerId.update((current) =>
      current === layerId ? null : layerId,
    );
  }

  protected toggleInfo(layerId: string): void {
    this.infoLayerId.update((current) =>
      current === layerId ? null : layerId,
    );
  }

  protected toggleFilter(layerId: string): void {
    this.filterLayerId.update((current) =>
      current === layerId ? null : layerId,
    );
  }

  private emitChange(
    overlay: MapOverlay,
    changes: Partial<MapOverlayStateChange>,
  ): void {
    this.overlayChanged.emit({
      layerId: overlay.id,
      visible: changes.visible ?? overlay.visible,
      opacity: changes.opacity ?? overlay.opacity,
      sortOrder: changes.sortOrder ?? overlay.sortOrder,
      filter:
        changes.filter === undefined
          ? overlay.filter
          : changes.filter,
    });
  }
}
