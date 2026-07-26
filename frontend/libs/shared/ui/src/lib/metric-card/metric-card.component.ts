import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';

export type MetricCardTone = 'default' | 'danger';

@Component({
  selector: 'fg-metric-card',
  standalone: true,
  templateUrl: './metric-card.component.html',
  styleUrl: './metric-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly tone = input<MetricCardTone>('default');
  readonly interactive = input(false);
  readonly activated = output<void>();
}
