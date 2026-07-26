import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LucideCircleAlert } from '@lucide/angular';

export type PageStateTone = 'error' | 'loading';

@Component({
  selector: 'fg-page-state',
  standalone: true,
  imports: [LucideCircleAlert],
  templateUrl: './page-state.component.html',
  styleUrl: './page-state.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageStateComponent {
  readonly title = input.required<string>();
  readonly description = input.required<string>();
  readonly tone = input<PageStateTone>('loading');
  readonly fullViewport = input(false);
}
