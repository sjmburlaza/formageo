using System.Text.Json;

namespace FormaGeo.Domain.Scoring;

public sealed class ScoringResult
{
    private ScoringResult()
    {
        // Required by Entity Framework Core.
    }

    private ScoringResult(
        Guid id,
        Guid siteId,
        Guid scoringScenarioId,
        Guid scoringModelId,
        decimal? overallScore,
        string rating,
        bool isScoreable,
        string breakdownJson,
        DateTimeOffset calculatedAtUtc)
    {
        Id = id;
        SiteId = siteId;
        ScoringScenarioId = scoringScenarioId;
        ScoringModelId = scoringModelId;
        OverallScore = overallScore;
        Rating = rating;
        IsScoreable = isScoreable;
        BreakdownJson = breakdownJson;
        CalculatedAtUtc = calculatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid ScoringScenarioId { get; private set; }

    public Guid ScoringModelId { get; private set; }

    public decimal? OverallScore { get; private set; }

    public string Rating { get; private set; } = string.Empty;

    public bool IsScoreable { get; private set; }

    public string BreakdownJson { get; private set; } = "{}";

    public DateTimeOffset CalculatedAtUtc { get; private set; }

    public static ScoringResult Create(
        Guid siteId,
        Guid scoringScenarioId,
        Guid scoringModelId,
        decimal? overallScore,
        string rating,
        bool isScoreable,
        string breakdownJson)
    {
        if (siteId == Guid.Empty ||
            scoringScenarioId == Guid.Empty ||
            scoringModelId == Guid.Empty)
        {
            throw new ArgumentException(
                "Site, scenario, and model IDs are required.");
        }

        if (overallScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overallScore),
                "Overall score must be between 0 and 100.");
        }

        if (isScoreable != overallScore.HasValue)
        {
            throw new ArgumentException(
                "A scoreable result must contain an overall score.");
        }

        if (string.IsNullOrWhiteSpace(rating))
        {
            throw new ArgumentException(
                "A rating is required.",
                nameof(rating));
        }

        try
        {
            using var document = JsonDocument.Parse(breakdownJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "The breakdown must be valid JSON.",
                nameof(breakdownJson),
                exception);
        }

        return new ScoringResult(
            Guid.NewGuid(),
            siteId,
            scoringScenarioId,
            scoringModelId,
            overallScore.HasValue
                ? decimal.Round(overallScore.Value, 2)
                : null,
            rating.Trim(),
            isScoreable,
            breakdownJson,
            DateTimeOffset.UtcNow);
    }
}
