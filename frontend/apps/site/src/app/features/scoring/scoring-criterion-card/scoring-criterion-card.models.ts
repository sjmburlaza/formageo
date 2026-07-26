import { SaveScoringCriterion, ScoringDirection } from '@frontend/models';

export type EditableCriterion = SaveScoringCriterion & {
  description: string;
  requiredAnalysis: string;
  recommendedDirection: ScoringDirection;
};

export type ScoringCriterionChange = {
  key: string;
  change: Partial<SaveScoringCriterion>;
};
