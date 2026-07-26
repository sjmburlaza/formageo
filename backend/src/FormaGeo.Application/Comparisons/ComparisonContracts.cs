using FormaGeo.Application.Scoring;
using FormaGeo.Domain.Scoring;

namespace FormaGeo.Application.Comparisons;

public sealed record CreateComparisonRequest(
    Guid ScoringScenarioId,
    IReadOnlyList<Guid> SiteIds,
    IReadOnlyList<string>? MetricKeys);

public sealed record ComparisonMetricResponse(
    string Key,
    string Name,
    string Unit,
    ScoringDirection Direction,
    string DataSource,
    decimal ConfiguredWeight);

public sealed record ComparisonDataVersionResponse(
    string CriterionKey,
    string DataSource,
    IReadOnlyList<string> Versions);

public sealed record ComparisonRankingResponse(
    int Rank,
    Guid SiteId,
    string SiteName,
    decimal? OverallScore,
    string Rating,
    bool IsScoreable,
    IReadOnlyList<CriterionScoreResponse> Metrics,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingInformation);

public sealed record ComparisonSnapshot(
    string ScenarioName,
    int ModelVersion,
    string ModelName,
    IReadOnlyList<Guid> SelectedSiteIds,
    IReadOnlyList<ComparisonMetricResponse> Metrics,
    IReadOnlyList<ComparisonRankingResponse> Rankings,
    IReadOnlyList<ComparisonDataVersionResponse> DataVersions,
    string RankingExplanation);

public sealed record ComparisonResponse(
    Guid Id,
    Guid ProjectId,
    Guid ScoringScenarioId,
    Guid ScoringModelId,
    string ScenarioName,
    int ModelVersion,
    string ModelName,
    IReadOnlyList<Guid> SelectedSiteIds,
    IReadOnlyList<ComparisonMetricResponse> Metrics,
    IReadOnlyList<ComparisonRankingResponse> Rankings,
    DateTimeOffset ComparisonDateUtc,
    IReadOnlyList<ComparisonDataVersionResponse> DataVersions,
    string RankingExplanation);

public sealed record ComparisonSummaryResponse(
    Guid Id,
    Guid ProjectId,
    Guid ScoringScenarioId,
    string ScenarioName,
    int ModelVersion,
    int SiteCount,
    string? LeadingSiteName,
    decimal? LeadingScore,
    DateTimeOffset ComparisonDateUtc);

public sealed class ComparisonValidationException : Exception
{
    public ComparisonValidationException(
        string field,
        string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
