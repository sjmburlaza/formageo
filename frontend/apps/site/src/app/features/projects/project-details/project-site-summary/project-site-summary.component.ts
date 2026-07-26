import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { SiteSummary } from '@frontend/models';
import { MetricCardComponent } from '@frontend/ui';
import { LucideCircleAlert } from '@lucide/angular';
import {
  formatArea,
  formatCentroid,
  formatCoordinate,
  formatPerimeter,
} from '../site-summary-formatters';

@Component({
  selector: 'fg-project-site-summary',
  standalone: true,
  imports: [LucideCircleAlert, MetricCardComponent],
  templateUrl: './project-site-summary.component.html',
  styleUrl: './project-site-summary.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectSiteSummaryComponent {
  readonly summary = input<SiteSummary | null>(null);
  readonly loading = input(false);
  readonly errorMessage = input<string | null>(null);

  readonly retryRequested = output<void>();
  readonly centroidRequested = output<SiteSummary>();

  protected readonly formatArea = formatArea;
  protected readonly formatCentroid = formatCentroid;
  protected readonly formatCoordinate = formatCoordinate;
  protected readonly formatPerimeter = formatPerimeter;
}
