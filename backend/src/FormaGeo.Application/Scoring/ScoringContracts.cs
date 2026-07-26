using FormaGeo.Domain.Scoring;

namespace FormaGeo.Application.Scoring;

public sealed record ScoringCriterionDefinitionResponse(
    string Key,
    string Name,
    string Description,
    ScoringDirection RecommendedDirection,
    NormalizationMethod RecommendedNormalizationMethod,
    string DataSource,
    string Unit,
    decimal DefaultLowerThreshold,
    decimal DefaultUpperThreshold,
    MissingDataBehavior DefaultMissingDataBehavior,
    string RequiredAnalysis);

public sealed record ScoringPresetResponse(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<ScoringPresetCriterionResponse> Criteria);

public sealed record ScoringPresetCriterionResponse(
    string CriterionKey,
    decimal Weight);

public sealed record ScoringCatalogResponse(
    decimal RequiredWeightTotal,
    IReadOnlyList<ScoringCriterionDefinitionResponse> Criteria,
    IReadOnlyList<ScoringPresetResponse> Presets);

public sealed record SaveScoringScenarioRequest(
    string Name,
    IReadOnlyList<SaveScoringCriterionRequest> Criteria);

public sealed record SaveScoringCriterionRequest(
    string Key,
    string Name,
    decimal Weight,
    ScoringDirection Direction,
    NormalizationMethod NormalizationMethod,
    string DataSource,
    string Unit,
    decimal LowerThreshold,
    decimal UpperThreshold,
    MissingDataBehavior MissingDataBehavior);

public sealed record RunScoringRequest(
    IReadOnlyList<Guid> SiteIds,
    int? ModelVersion);

public sealed record ScoringCriterionResponse(
    Guid Id,
    string Key,
    string Name,
    decimal Weight,
    ScoringDirection Direction,
    NormalizationMethod NormalizationMethod,
    string DataSource,
    string Unit,
    decimal LowerThreshold,
    decimal UpperThreshold,
    MissingDataBehavior MissingDataBehavior,
    int SortOrder);

public sealed record ScoringModelResponse(
    Guid Id,
    Guid ScoringScenarioId,
    int Version,
    string Name,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ScoringCriterionResponse> Criteria)
{
    public static ScoringModelResponse FromDomain(
        ScoringModel model)
    {
        return new ScoringModelResponse(
            model.Id,
            model.ScoringScenarioId,
            model.Version,
            model.Name,
            model.CreatedAtUtc,
            model.Criteria
                .OrderBy(criterion => criterion.SortOrder)
                .Select(criterion =>
                    new ScoringCriterionResponse(
                        criterion.Id,
                        criterion.Key,
                        criterion.Name,
                        criterion.Weight,
                        criterion.Direction,
                        criterion.NormalizationMethod,
                        criterion.DataSource,
                        criterion.Unit,
                        criterion.LowerThreshold,
                        criterion.UpperThreshold,
                        criterion.MissingDataBehavior,
                        criterion.SortOrder))
                .ToArray());
    }
}

public sealed record ScoringScenarioResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ScoringModelResponse LatestModel);

public sealed record CriterionScoreResponse(
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

public sealed record ScoringBreakdownSnapshot(
    string SiteName,
    decimal ConfiguredWeightTotal,
    decimal EffectiveWeightTotal,
    IReadOnlyList<CriterionScoreResponse> Criteria,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<string> ValidationMessages);

public sealed record ScoringResultResponse(
    Guid Id,
    Guid SiteId,
    string SiteName,
    Guid ScoringScenarioId,
    Guid ScoringModelId,
    int ModelVersion,
    string ModelName,
    decimal? OverallScore,
    string Rating,
    bool IsScoreable,
    DateTimeOffset CalculatedAtUtc,
    decimal ConfiguredWeightTotal,
    decimal EffectiveWeightTotal,
    IReadOnlyList<CriterionScoreResponse> Criteria,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingInformation,
    IReadOnlyList<string> ValidationMessages);
