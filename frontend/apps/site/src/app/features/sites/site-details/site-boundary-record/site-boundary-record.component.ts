import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { Site } from '@frontend/models';

@Component({
  selector: 'fg-site-boundary-record',
  standalone: true,
  templateUrl: './site-boundary-record.component.html',
  styleUrl: './site-boundary-record.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteBoundaryRecordComponent {
  readonly site = input.required<Site>();

  protected readonly formattedBoundary = computed(() =>
    JSON.stringify(this.site().boundary, null, 2),
  );
}
