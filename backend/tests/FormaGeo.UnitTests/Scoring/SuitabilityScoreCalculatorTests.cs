using FormaGeo.Domain.Scoring;

namespace FormaGeo.UnitTests.Scoring;

public sealed class SuitabilityScoreCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsTransparentContributionsThatSumToTotal()
    {
        var criteria = new[]
        {
            Criterion(
                "flood",
                60,
                ScoringDirection.LowerIsBetter,
                0,
                100),
            Criterion(
                "access",
                40,
                ScoringDirection.LowerIsBetter,
                0,
                100)
        };
        var observations = new[]
        {
            new CriterionObservation("flood", 25, "2026.07", null),
            new CriterionObservation("access", 50, "2.0.0", null)
        };

        var result = SuitabilityScoreCalculator.Calculate(
            criteria,
            observations);

        Assert.True(result.IsScoreable);
        Assert.Equal(65m, result.OverallScore);
        Assert.Equal(
            result.OverallScore,
            result.Criteria.Sum(criterion =>
                criterion.Contribution));
        Assert.Equal(45m, result.Criteria[0].Contribution);
        Assert.Equal(20m, result.Criteria[1].Contribution);
        Assert.All(
            result.Criteria,
            criterion => Assert.False(criterion.IsMissing));
    }

    [Fact]
    public void Calculate_ReconcilesRoundedContributionsToOneHundred()
    {
        var criteria = new[]
        {
            Criterion(
                "one",
                33.3333m,
                ScoringDirection.HigherIsBetter,
                0,
                100),
            Criterion(
                "two",
                33.3333m,
                ScoringDirection.HigherIsBetter,
                0,
                100),
            Criterion(
                "three",
                33.3334m,
                ScoringDirection.HigherIsBetter,
                0,
                100)
        };
        var observations = criteria
            .Select(criterion =>
                new CriterionObservation(
                    criterion.Key,
                    100,
                    "v1",
                    null))
            .ToArray();

        var result = SuitabilityScoreCalculator.Calculate(
            criteria,
            observations);

        Assert.Equal(100m, result.OverallScore);
        Assert.Equal(
            result.OverallScore,
            result.Criteria.Sum(criterion =>
                criterion.Contribution));
    }

    [Fact]
    public void Calculate_ExcludesMissingDataAndReweightsExplicitly()
    {
        var criteria = new[]
        {
            Criterion(
                "available",
                75,
                ScoringDirection.HigherIsBetter,
                0,
                100),
            Criterion(
                "missing",
                25,
                ScoringDirection.HigherIsBetter,
                0,
                100,
                MissingDataBehavior.ExcludeAndReweight)
        };
        var observations = new[]
        {
            new CriterionObservation(
                "available",
                80,
                "v1",
                null),
            new CriterionObservation(
                "missing",
                null,
                null,
                "Dataset is unavailable.")
        };

        var result = SuitabilityScoreCalculator.Calculate(
            criteria,
            observations);

        Assert.True(result.IsScoreable);
        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(100m, result.Criteria[0].EffectiveWeight);
        Assert.Equal(0m, result.Criteria[1].EffectiveWeight);
        Assert.Contains(
            result.ValidationMessages,
            message => message.Contains(
                "redistributed",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.MissingInformation,
            message => message.Contains(
                "excluded",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calculate_RequiredMissingDataMakesSiteUnscoreable()
    {
        var criteria = new[]
        {
            Criterion(
                "required",
                100,
                ScoringDirection.HigherIsBetter,
                0,
                100,
                MissingDataBehavior.CannotScore)
        };

        var result = SuitabilityScoreCalculator.Calculate(
            criteria,
            [
                new CriterionObservation(
                    "required",
                    null,
                    null,
                    "Run the required analysis.")
            ]);

        Assert.False(result.IsScoreable);
        Assert.Null(result.OverallScore);
        Assert.Equal("Not scoreable", result.Rating);
        Assert.Single(result.MissingInformation);
        Assert.All(
            result.Criteria,
            criterion => Assert.Equal(
                0m,
                criterion.Contribution));
    }

    [Fact]
    public void ValidateCriteria_RejectsWeightsThatDoNotTotalOneHundred()
    {
        var criteria = new[]
        {
            Criterion(
                "one",
                60,
                ScoringDirection.HigherIsBetter,
                0,
                100),
            Criterion(
                "two",
                30,
                ScoringDirection.HigherIsBetter,
                0,
                100)
        };

        var exception = Assert.Throws<ScoringValidationException>(
            () => ScoringModel.ValidateCriteria(criteria));

        Assert.Equal("criteria.weight", exception.Field);
        Assert.Contains("90", exception.Message);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(25, 75)]
    [InlineData(100, 0)]
    public void Normalize_RespectsLowerIsBetterDirection(
        decimal value,
        decimal expected)
    {
        var criterion = Criterion(
            "lower",
            100,
            ScoringDirection.LowerIsBetter,
            0,
            100);

        var result = SuitabilityScoreCalculator.Normalize(
            value,
            criterion);

        Assert.Equal(expected, result);
    }

    private static ScoringCriterion Criterion(
        string key,
        decimal weight,
        ScoringDirection direction,
        decimal lower,
        decimal upper,
        MissingDataBehavior missingDataBehavior =
            MissingDataBehavior.CannotScore)
    {
        return ScoringCriterion.Create(
            key,
            key,
            weight,
            direction,
            NormalizationMethod.Linear,
            "Test source",
            "%",
            lower,
            upper,
            missingDataBehavior,
            0);
    }
}
