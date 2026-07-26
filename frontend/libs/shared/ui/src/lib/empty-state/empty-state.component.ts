import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import {
  LucideDatabase,
  LucideLandPlot,
  LucideLayers3,
  LucideMapPinOff,
  LucideMountain,
  LucideSearchX,
} from '@lucide/angular';

export type EmptyStateAppearance = 'bordered' | 'plain';
export type EmptyStateIcon =
  | 'data'
  | 'layers'
  | 'map'
  | 'projects'
  | 'search'
  | 'terrain';

@Component({
  selector: 'fg-empty-state',
  standalone: true,
  imports: [
    LucideDatabase,
    LucideLandPlot,
    LucideLayers3,
    LucideMapPinOff,
    LucideMountain,
    LucideSearchX,
  ],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input.required<string>();
  readonly appearance = input<EmptyStateAppearance>('plain');
  readonly icon = input<EmptyStateIcon>('layers');
  readonly headingLevel = input<2 | 3 | 4>(3);
  readonly compact = input(false);
}
