import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type StatusBadgeTone =
  | 'error'
  | 'info'
  | 'neutral'
  | 'success'
  | 'warning';

@Component({
  selector: 'fg-status-badge',
  standalone: true,
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  readonly label = input.required<string>();
  readonly tone = input<StatusBadgeTone>('info');
}
