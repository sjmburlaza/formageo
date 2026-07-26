import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import {
  LucideCircleAlert,
  LucideCircleCheck,
  LucideInfo,
  LucideTriangleAlert,
} from '@lucide/angular';

export type AlertTone = 'error' | 'info' | 'success' | 'warning';

@Component({
  selector: 'fg-alert',
  standalone: true,
  imports: [
    LucideCircleAlert,
    LucideCircleCheck,
    LucideInfo,
    LucideTriangleAlert,
  ],
  templateUrl: './alert.component.html',
  styleUrl: './alert.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertComponent {
  readonly message = input.required<string>();
  readonly tone = input<AlertTone>('info');
  readonly actionLabel = input<string | null>(null);
  readonly actionTriggered = output<void>();
}
