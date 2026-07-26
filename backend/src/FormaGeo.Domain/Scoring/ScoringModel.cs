namespace FormaGeo.Domain.Scoring;

public sealed class ScoringModel
{
    private readonly List<ScoringCriterion> _criteria = [];

    private ScoringModel()
    {
        // Required by Entity Framework Core.
    }

    private ScoringModel(
        Guid id,
        Guid scoringScenarioId,
        int version,
        string name,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ScoringScenarioId = scoringScenarioId;
        Version = version;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ScoringScenarioId { get; private set; }

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<ScoringCriterion> Criteria => _criteria;

    public static ScoringModel Create(
        Guid scoringScenarioId,
        int version,
        string name,
        IEnumerable<ScoringCriterion> criteria)
    {
        if (scoringScenarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "A scoring scenario ID is required.",
                nameof(scoringScenarioId));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A model version must be greater than zero.");
        }

        var criterionList = criteria.ToList();
        ValidateCriteria(criterionList);

        var model = new ScoringModel(
            Guid.NewGuid(),
            scoringScenarioId,
            version,
            NormalizeName(name),
            DateTimeOffset.UtcNow);
        model._criteria.AddRange(criterionList);

        return model;
    }

    public static void ValidateCriteria(
        IReadOnlyCollection<ScoringCriterion> criteria)
    {
        if (criteria.Count == 0)
        {
            throw new ScoringValidationException(
                "criteria",
                "Select at least one scoring criterion.");
        }

        var duplicateKey = criteria
            .GroupBy(
                criterion => criterion.Key,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateKey is not null)
        {
            throw new ScoringValidationException(
                "criteria",
                $"Criterion '{duplicateKey}' can only be added once.");
        }

        var weightTotal = criteria.Sum(criterion => criterion.Weight);

        if (Math.Abs(weightTotal - 100m) > 0.0001m)
        {
            throw new ScoringValidationException(
                "criteria.weight",
                $"Criterion weights must total 100. The current total is {weightTotal:0.##}.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ScoringValidationException(
                "name",
                "A scoring model name is required.");
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > 200)
        {
            throw new ScoringValidationException(
                "name",
                "A scoring model name cannot exceed 200 characters.");
        }

        return normalizedName;
    }
}

public sealed class ScoringValidationException : Exception
{
    public ScoringValidationException(
        string field,
        string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
