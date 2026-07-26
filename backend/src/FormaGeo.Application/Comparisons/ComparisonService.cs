using FormaGeo.Application.Projects;
using FormaGeo.Application.Scoring;
using FormaGeo.Domain.Comparisons;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormaGeo.Application.Comparisons;

public sealed class ComparisonService
{
    public const int MinimumSiteCount = 2;
    public const int MaximumSiteCount = 5;

    private const string RankingExplanation =
        "Sites are ordered by overall score from highest to lowest. " +
        "Each score is the sum of normalized metric contributions under the " +
        "same saved scenario version. Unscoreable sites appear last, and " +
        "equal scores share the same rank.";

    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private readonly IComparisonRepository _comparisonRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ScoringService _scoringService;

    public ComparisonService(
        IComparisonRepository comparisonRepository,
        IProjectRepository projectRepository,
        ScoringService scoringService)
    {
        _comparisonRepository = comparisonRepository;
        _projectRepository = projectRepository;
        _scoringService = scoringService;
    }

    public async Task<ComparisonResponse> CreateAsync(
        Guid projectId,
        CreateComparisonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _projectRepository.ExistsAsync(
            projectId,
            cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        if (request.SiteIds is null)
        {
            throw new ComparisonValidationException(
                "siteIds",
                "Select between two and five sites to compare.");
        }

        var siteIds = request.SiteIds
            .Distinct()
            .ToArray();

        if (siteIds.Length is < MinimumSiteCount or > MaximumSiteCount)
        {
            throw new ComparisonValidationException(
                "siteIds",
                "Select between two and five distinct sites to compare.");
        }

        var scenario = await _scoringService.GetAsync(
            request.ScoringScenarioId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{request.ScoringScenarioId}' was not found.");

        if (scenario.ProjectId != projectId)
        {
            throw new ComparisonValidationException(
                "scoringScenarioId",
                "The scoring scenario must belong to the comparison project.");
        }

        var availableMetrics = scenario.LatestModel.Criteria
            .OrderBy(criterion => criterion.SortOrder)
            .ToArray();
        var requestedKeys = request.MetricKeys?
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var metricKeys = requestedKeys.Length == 0
            ? availableMetrics
                .Select(criterion => criterion.Key)
                .ToArray()
            : requestedKeys;
        var availableKeys = availableMetrics
            .Select(criterion => criterion.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownKey = metricKeys.FirstOrDefault(
            key => !availableKeys.Contains(key));

        if (unknownKey is not null)
        {
            throw new ComparisonValidationException(
                "metricKeys",
                $"Metric '{unknownKey}' is not part of the selected scenario.");
        }

        var results = await _scoringService.RunAsync(
            scenario.Id,
            new RunScoringRequest(
                siteIds,
                scenario.LatestModel.Version),
            cancellationToken);
        var selectedKeySet = metricKeys.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var metrics = availableMetrics
            .Where(criterion => selectedKeySet.Contains(criterion.Key))
            .Select(criterion => new ComparisonMetricResponse(
                criterion.Key,
                criterion.Name,
                criterion.Unit,
                criterion.Direction,
                criterion.DataSource,
                criterion.Weight))
            .ToArray();
        var rankings = Rank(results, selectedKeySet);
        var dataVersions = BuildDataVersions(
            results,
            selectedKeySet);
        var snapshot = new ComparisonSnapshot(
            scenario.Name,
            scenario.LatestModel.Version,
            scenario.LatestModel.Name,
            siteIds,
            metrics,
            rankings,
            dataVersions,
            RankingExplanation);
        var comparison = SiteComparison.Create(
            projectId,
            scenario.Id,
            scenario.LatestModel.Id,
            JsonSerializer.Serialize(
                snapshot,
                SnapshotJsonOptions));

        await _comparisonRepository.AddAsync(
            comparison,
            cancellationToken);

        return ToResponse(comparison, snapshot);
    }

    public async Task<ComparisonResponse?> GetAsync(
        Guid comparisonId,
        CancellationToken cancellationToken = default)
    {
        var comparison = await _comparisonRepository.GetByIdAsync(
            comparisonId,
            cancellationToken);

        return comparison is null
            ? null
            : ToResponse(
                comparison,
                DeserializeSnapshot(comparison));
    }

    public async Task<IReadOnlyList<ComparisonSummaryResponse>>
        GetForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        if (!await _projectRepository.ExistsAsync(
            projectId,
            cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        var comparisons =
            await _comparisonRepository.GetByProjectIdAsync(
                projectId,
                cancellationToken);

        return comparisons
            .Select(comparison =>
            {
                var snapshot = DeserializeSnapshot(comparison);
                var leader = snapshot.Rankings.FirstOrDefault(
                    ranking => ranking.Rank == 1);

                return new ComparisonSummaryResponse(
                    comparison.Id,
                    comparison.ProjectId,
                    comparison.ScoringScenarioId,
                    snapshot.ScenarioName,
                    snapshot.ModelVersion,
                    snapshot.SelectedSiteIds.Count,
                    leader?.SiteName,
                    leader?.OverallScore,
                    comparison.ComparisonDateUtc);
            })
            .ToArray();
    }

    internal static IReadOnlyList<ComparisonRankingResponse> Rank(
        IReadOnlyList<ScoringResultResponse> results,
        IReadOnlySet<string> selectedMetricKeys)
    {
        var ordered = results
            .OrderByDescending(result => result.IsScoreable)
            .ThenByDescending(result => result.OverallScore)
            .ThenBy(result => result.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rankings = new List<ComparisonRankingResponse>(ordered.Length);
        decimal? previousScore = null;
        var previousScoreable = false;
        var currentRank = 0;

        for (var index = 0; index < ordered.Length; index++)
        {
            var result = ordered[index];
            if (index == 0 ||
                result.IsScoreable != previousScoreable ||
                result.OverallScore != previousScore)
            {
                currentRank = index + 1;
            }

            rankings.Add(new ComparisonRankingResponse(
                currentRank,
                result.SiteId,
                result.SiteName,
                result.OverallScore,
                result.Rating,
                result.IsScoreable,
                result.Criteria
                    .Where(metric =>
                        selectedMetricKeys.Contains(metric.CriterionKey))
                    .ToArray(),
                result.Strengths,
                result.Weaknesses,
                result.MissingInformation));
            previousScore = result.OverallScore;
            previousScoreable = result.IsScoreable;
        }

        return rankings;
    }

    private static IReadOnlyList<ComparisonDataVersionResponse>
        BuildDataVersions(
            IReadOnlyList<ScoringResultResponse> results,
            IReadOnlySet<string> selectedMetricKeys)
    {
        return results
            .SelectMany(result => result.Criteria)
            .Where(metric =>
                selectedMetricKeys.Contains(metric.CriterionKey))
            .GroupBy(
                metric => new
                {
                    metric.CriterionKey,
                    metric.DataSource
                })
            .Select(group =>
                new ComparisonDataVersionResponse(
                    group.Key.CriterionKey,
                    group.Key.DataSource,
                    group
                        .Select(metric =>
                            metric.DataVersion ?? "Unavailable")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(version => version)
                        .ToArray()))
            .OrderBy(version => version.CriterionKey)
            .ToArray();
    }

    private static ComparisonSnapshot DeserializeSnapshot(
        SiteComparison comparison)
    {
        return JsonSerializer.Deserialize<ComparisonSnapshot>(
            comparison.SnapshotJson,
            SnapshotJsonOptions)
            ?? throw new InvalidOperationException(
                $"Comparison '{comparison.Id}' has no readable snapshot.");
    }

    private static ComparisonResponse ToResponse(
        SiteComparison comparison,
        ComparisonSnapshot snapshot)
    {
        return new ComparisonResponse(
            comparison.Id,
            comparison.ProjectId,
            comparison.ScoringScenarioId,
            comparison.ScoringModelId,
            snapshot.ScenarioName,
            snapshot.ModelVersion,
            snapshot.ModelName,
            snapshot.SelectedSiteIds,
            snapshot.Metrics,
            snapshot.Rankings,
            comparison.ComparisonDateUtc,
            snapshot.DataVersions,
            snapshot.RankingExplanation);
    }
}
