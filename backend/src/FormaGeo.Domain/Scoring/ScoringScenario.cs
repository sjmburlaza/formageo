namespace FormaGeo.Domain.Scoring;

public sealed class ScoringScenario
{
    private ScoringScenario()
    {
        // Required by Entity Framework Core.
    }

    private ScoringScenario(
        Guid id,
        Guid projectId,
        string name,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ScoringScenario Create(
        Guid projectId,
        string name)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "A project ID is required.",
                nameof(projectId));
        }

        var now = DateTimeOffset.UtcNow;
        return new ScoringScenario(
            Guid.NewGuid(),
            projectId,
            NormalizeName(name),
            now,
            now);
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ScoringValidationException(
                "name",
                "A scenario name is required.");
        }

        var normalized = name.Trim();

        if (normalized.Length > 200)
        {
            throw new ScoringValidationException(
                "name",
                "A scenario name cannot exceed 200 characters.");
        }

        return normalized;
    }
}
