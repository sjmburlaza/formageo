using FormaGeo.Application.Comparisons;
using FormaGeo.Application.Scoring;

namespace FormaGeo.UnitTests.Comparisons;

public sealed class ComparisonRankingTests
{
    [Fact]
    public void Rank_OrdersScoresSharesTiesAndPlacesUnscoreableLast()
    {
        var results = new[]
        {
            Result("Alpha", 75m),
            Result("Bravo", null),
            Result("Charlie", 88m),
            Result("Delta", 75m)
        };

        var rankings = ComparisonService.Rank(
            results,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "flood-risk"
            });

        Assert.Collection(
            rankings,
            ranking =>
            {
                Assert.Equal("Charlie", ranking.SiteName);
                Assert.Equal(1, ranking.Rank);
            },
            ranking =>
            {
                Assert.Equal("Alpha", ranking.SiteName);
                Assert.Equal(2, ranking.Rank);
            },
            ranking =>
            {
                Assert.Equal("Delta", ranking.SiteName);
                Assert.Equal(2, ranking.Rank);
            },
            ranking =>
            {
                Assert.Equal("Bravo", ranking.SiteName);
                Assert.Equal(4, ranking.Rank);
                Assert.False(ranking.IsScoreable);
            });
    }

    [Fact]
    public void Rank_OnlyIncludesSelectedMetrics()
    {
        var rankings = ComparisonService.Rank(
            [Result("Alpha", 75m)],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "flood-risk"
            });

        var ranking = Assert.Single(rankings);
        var metric = Assert.Single(ranking.Metrics);
        Assert.Equal("flood-risk", metric.CriterionKey);
    }

    private static ScoringResultResponse Result(
        string siteName,
        decimal? score)
    {
        var siteId = Guid.NewGuid();
        var scoreable = score.HasValue;

        return new ScoringResultResponse(
            Guid.NewGuid(),
            siteId,
            siteName,
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "Balanced",
            score,
            scoreable ? "Suitable" : "Cannot score",
            scoreable,
            DateTimeOffset.UtcNow,
            100m,
            scoreable ? 100m : 0m,
            [
                new CriterionScoreResponse(
                    "flood-risk",
                    "Flood exposure",
                    12m,
                    "%",
                    88m,
                    50m,
                    50m,
                    44m,
                    "Flood susceptibility",
                    "2026.07",
                    false,
                    Domain.Scoring.MissingDataBehavior.ExcludeAndReweight,
                    "Lower flood exposure produces a higher score."),
                new CriterionScoreResponse(
                    "slope",
                    "Average slope",
                    5m,
                    "°",
                    80m,
                    50m,
                    50m,
                    40m,
                    "Terrain model",
                    "2026.06",
                    false,
                    Domain.Scoring.MissingDataBehavior.ExcludeAndReweight,
                    "Lower slope produces a higher score.")
            ],
            [],
            [],
            [],
            []);
    }
}
