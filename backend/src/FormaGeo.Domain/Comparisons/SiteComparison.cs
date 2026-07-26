using System.Text.Json;

namespace FormaGeo.Domain.Comparisons;

public sealed class SiteComparison
{
    private SiteComparison()
    {
        // Required by Entity Framework Core.
    }

    private SiteComparison(
        Guid id,
        Guid projectId,
        Guid scoringScenarioId,
        Guid scoringModelId,
        string snapshotJson,
        DateTimeOffset comparisonDateUtc)
    {
        Id = id;
        ProjectId = projectId;
        ScoringScenarioId = scoringScenarioId;
        ScoringModelId = scoringModelId;
        SnapshotJson = snapshotJson;
        ComparisonDateUtc = comparisonDateUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid ScoringScenarioId { get; private set; }

    public Guid ScoringModelId { get; private set; }

    public string SnapshotJson { get; private set; } = "{}";

    public DateTimeOffset ComparisonDateUtc { get; private set; }

    public static SiteComparison Create(
        Guid projectId,
        Guid scoringScenarioId,
        Guid scoringModelId,
        string snapshotJson)
    {
        if (projectId == Guid.Empty ||
            scoringScenarioId == Guid.Empty ||
            scoringModelId == Guid.Empty)
        {
            throw new ArgumentException(
                "Project, scenario, and model IDs are required.");
        }

        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "The comparison snapshot must be valid JSON.",
                nameof(snapshotJson),
                exception);
        }

        return new SiteComparison(
            Guid.NewGuid(),
            projectId,
            scoringScenarioId,
            scoringModelId,
            snapshotJson,
            DateTimeOffset.UtcNow);
    }
}
