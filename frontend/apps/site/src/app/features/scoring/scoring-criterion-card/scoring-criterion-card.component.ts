import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import {
  MissingDataBehavior,
  NormalizationMethod,
  SaveScoringCriterion,
  ScoringDirection,
} from '@frontend/models';
import { LucideTrash2 } from '@lucide/angular';
import {
  EditableCriterion,
  ScoringCriterionChange,
} from './scoring-criterion-card.models';

@Component({
  selector: 'fg-scoring-criterion-card',
  standalone: true,
  imports: [LucideTrash2],
  templateUrl: './scoring-criterion-card.component.html',
  styleUrl: './scoring-criterion-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScoringCriterionCardComponent {
  readonly criterion = input.required<EditableCriterion>();
  readonly index = input.required<number>();

  readonly removed = output<string>();
  readonly changed = output<ScoringCriterionChange>();

  protected updateWeight(value: string): void {
    this.emitChange({ weight: this.numberValue(value) });
  }

  protected updateDirection(direction: ScoringDirection): void {
    this.emitChange({ direction });
  }

  protected updateNormalization(
    normalizationMethod: NormalizationMethod,
  ): void {
    this.emitChange({ normalizationMethod });
  }

  protected updateLowerThreshold(value: string): void {
    this.emitChange({ lowerThreshold: this.numberValue(value) });
  }

  protected updateUpperThreshold(value: string): void {
    this.emitChange({ upperThreshold: this.numberValue(value) });
  }

  protected updateMissingBehavior(
    missingDataBehavior: MissingDataBehavior,
  ): void {
    this.emitChange({ missingDataBehavior });
  }

  private emitChange(change: Partial<SaveScoringCriterion>): void {
    this.changed.emit({ key: this.criterion().key, change });
  }

  private numberValue(value: string): number {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
  }
}
