namespace FormaGeo.Domain.Scoring;

public sealed record CriterionObservation(
    string CriterionKey,
    decimal? Value,
    string? DataVersion,
    string? MissingReason);

public sealed record CriterionScore(
    string CriterionKey,
    string Name,
    decimal? RawValue,
    string Unit,
    decimal? NormalizedScore,
    decimal ConfiguredWeight,
    decimal EffectiveWeight,
    decimal Contribution,
    string DataSource,
    string? DataVersion,
    bool IsMissing,
    MissingDataBehavior MissingDataBehavior,
    string Explanation);

public sealed record SuitabilityScoreCalculation(
    decimal? OverallScore,
    bool IsScoreable,
    string Rating,
    IReadOnlyList<CriterionScore> Criteria,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<string> ValidationMessages);

public static class SuitabilityScoreCalculator
{
    public static SuitabilityScoreCalculation Calculate(
        IReadOnlyCollection<ScoringCriterion> criteria,
        IReadOnlyCollection<CriterionObservation> observations)
    {
        ScoringModel.ValidateCriteria(criteria);

        var observationsByKey = observations.ToDictionary(
            observation => observation.CriterionKey,
            StringComparer.OrdinalIgnoreCase);
        var missingCannotScore = criteria
            .Where(criterion =>
                (!observationsByKey.TryGetValue(
                    criterion.Key,
                    out var observation) ||
                 !observation.Value.HasValue) &&
                criterion.MissingDataBehavior ==
                    MissingDataBehavior.CannotScore)
            .ToArray();
        var availableWeight = criteria
            .Where(criterion =>
                observationsByKey.TryGetValue(
                    criterion.Key,
                    out var observation) &&
                observation.Value.HasValue)
            .Sum(criterion => criterion.Weight);
        var excludedWeight = criteria
            .Where(criterion =>
                (!observationsByKey.TryGetValue(
                    criterion.Key,
                    out var observation) ||
                 !observation.Value.HasValue) &&
                criterion.MissingDataBehavior ==
                    MissingDataBehavior.ExcludeAndReweight)
            .Sum(criterion => criterion.Weight);
        var includedWeight = 100m - excludedWeight;
        var scoreable =
            missingCannotScore.Length == 0 &&
            includedWeight > 0 &&
            (availableWeight > 0 ||
             criteria.Any(criterion =>
                 criterion.MissingDataBehavior ==
                    MissingDataBehavior.ScoreZero));
        var criterionScores = new List<CriterionScore>();

        foreach (var criterion in criteria.OrderBy(item => item.SortOrder))
        {
            observationsByKey.TryGetValue(
                criterion.Key,
                out var observation);
            var isMissing = !observation?.Value.HasValue ?? true;
            var effectiveWeight =
                isMissing &&
                criterion.MissingDataBehavior ==
                    MissingDataBehavior.ExcludeAndReweight
                    ? 0m
                    : scoreable
                        ? criterion.Weight / includedWeight * 100m
                        : criterion.Weight;
            decimal? normalizedScore = null;
            var contribution = 0m;
            string explanation;

            if (isMissing)
            {
                explanation = MissingExplanation(
                    criterion,
                    observation?.MissingReason);

                if (criterion.MissingDataBehavior ==
                    MissingDataBehavior.ScoreZero)
                {
                    normalizedScore = 0m;
                }
            }
            else
            {
                normalizedScore = Normalize(
                    observation!.Value!.Value,
                    criterion);
                contribution = scoreable
                    ? decimal.Round(
                        normalizedScore.Value *
                        effectiveWeight / 100m,
                        2)
                    : 0m;
                explanation = ValueExplanation(
                    criterion,
                    observation.Value.Value,
                    normalizedScore.Value);
            }

            criterionScores.Add(new CriterionScore(
                criterion.Key,
                criterion.Name,
                observation?.Value,
                criterion.Unit,
                normalizedScore,
                criterion.Weight,
                decimal.Round(effectiveWeight, 4),
                contribution,
                criterion.DataSource,
                observation?.DataVersion,
                isMissing,
                criterion.MissingDataBehavior,
                explanation));
        }

        decimal? overallScore = null;
        if (scoreable)
        {
            overallScore = decimal.Round(
                criterionScores.Sum(item =>
                    (item.NormalizedScore ?? 0m) *
                    item.EffectiveWeight / 100m),
                2);
            var displayedTotal = criterionScores.Sum(
                item => item.Contribution);
            var roundingDifference =
                overallScore.Value - displayedTotal;
            var lastContributingIndex =
                criterionScores.FindLastIndex(item =>
                    item.NormalizedScore.HasValue &&
                    item.EffectiveWeight > 0);

            if (roundingDifference != 0 &&
                lastContributingIndex >= 0)
            {
                criterionScores[lastContributingIndex] =
                    criterionScores[lastContributingIndex] with
                    {
                        Contribution = decimal.Round(
                            criterionScores[lastContributingIndex]
                                .Contribution + roundingDifference,
                            2)
                    };
            }
        }

        var strengths = criterionScores
            .Where(item => item.NormalizedScore >= 70)
            .OrderByDescending(item => item.Contribution)
            .Select(item =>
                $"{item.Name} scored {item.NormalizedScore:0.#}/100.")
            .Take(3)
            .ToArray();
        var weaknesses = criterionScores
            .Where(item =>
                item.NormalizedScore.HasValue &&
                item.NormalizedScore < 50)
            .OrderBy(item => item.NormalizedScore)
            .Select(item =>
                $"{item.Name} scored {item.NormalizedScore:0.#}/100.")
            .Take(3)
            .ToArray();
        var missingInformation = criterionScores
            .Where(item => item.IsMissing)
            .Select(item => item.Explanation)
            .ToArray();
        var validationMessages = new List<string>();

        if (excludedWeight > 0 && scoreable)
        {
            validationMessages.Add(
                $"{excludedWeight:0.##}% of configured weight was redistributed because data was missing.");
        }

        if (!scoreable)
        {
            validationMessages.Add(
                missingCannotScore.Length > 0
                    ? "The site cannot be scored because required criterion data is missing."
                    : "The site cannot be scored because no weighted criterion data is available.");
        }

        return new SuitabilityScoreCalculation(
            overallScore,
            scoreable,
            Rating(overallScore),
            criterionScores,
            strengths,
            weaknesses,
            missingInformation,
            validationMessages);
    }

    public static decimal Normalize(
        decimal value,
        ScoringCriterion criterion)
    {
        decimal score;

        if (criterion.NormalizationMethod ==
            NormalizationMethod.Boolean)
        {
            var truthy = value > 0;
            score = criterion.Direction ==
                ScoringDirection.HigherIsBetter
                ? truthy ? 100m : 0m
                : truthy ? 0m : 100m;
        }
        else if (criterion.NormalizationMethod ==
            NormalizationMethod.Threshold)
        {
            score = criterion.Direction ==
                ScoringDirection.HigherIsBetter
                ? value >= criterion.UpperThreshold
                    ? 100m
                    : value <= criterion.LowerThreshold
                        ? 0m
                        : 50m
                : value <= criterion.LowerThreshold
                    ? 100m
                    : value >= criterion.UpperThreshold
                        ? 0m
                        : 50m;
        }
        else
        {
            var ratio =
                (value - criterion.LowerThreshold) /
                (criterion.UpperThreshold -
                 criterion.LowerThreshold);
            score = criterion.Direction ==
                ScoringDirection.HigherIsBetter
                ? ratio * 100m
                : (1m - ratio) * 100m;
        }

        return decimal.Round(
            Math.Clamp(score, 0m, 100m),
            2);
    }

    private static string Rating(decimal? score)
    {
        return score switch
        {
            null => "Not scoreable",
            >= 80m => "Excellent fit",
            >= 65m => "Good fit",
            >= 50m => "Moderate fit",
            >= 35m => "Marginal fit",
            _ => "Poor fit"
        };
    }

    private static string MissingExplanation(
        ScoringCriterion criterion,
        string? reason)
    {
        var handling = criterion.MissingDataBehavior switch
        {
            MissingDataBehavior.ExcludeAndReweight =>
                "It was excluded and the remaining weights were rebalanced.",
            MissingDataBehavior.ScoreZero =>
                "It contributes zero points.",
            MissingDataBehavior.CannotScore =>
                "This criterion is required, so the site cannot be scored.",
            _ => string.Empty
        };

        return $"{criterion.Name}: " +
            $"{reason ?? "No usable value was found."} {handling}";
    }

    private static string ValueExplanation(
        ScoringCriterion criterion,
        decimal value,
        decimal score)
    {
        var direction = criterion.Direction ==
            ScoringDirection.HigherIsBetter
            ? "higher values are preferred"
            : "lower values are preferred";

        return
            $"{value:0.##}{criterion.Unit} normalized to " +
            $"{score:0.##}/100 using {criterion.NormalizationMethod} " +
            $"between {criterion.LowerThreshold:0.##} and " +
            $"{criterion.UpperThreshold:0.##}{criterion.Unit}; {direction}.";
    }
}
