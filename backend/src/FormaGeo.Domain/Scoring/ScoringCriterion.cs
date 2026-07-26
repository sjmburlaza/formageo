namespace FormaGeo.Domain.Scoring;

public sealed class ScoringCriterion
{
    private ScoringCriterion()
    {
        // Required by Entity Framework Core.
    }

    private ScoringCriterion(
        Guid id,
        string key,
        string name,
        decimal weight,
        ScoringDirection direction,
        NormalizationMethod normalizationMethod,
        string dataSource,
        string unit,
        decimal lowerThreshold,
        decimal upperThreshold,
        MissingDataBehavior missingDataBehavior,
        int sortOrder)
    {
        Id = id;
        Key = key;
        Name = name;
        Weight = weight;
        Direction = direction;
        NormalizationMethod = normalizationMethod;
        DataSource = dataSource;
        Unit = unit;
        LowerThreshold = lowerThreshold;
        UpperThreshold = upperThreshold;
        MissingDataBehavior = missingDataBehavior;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid ScoringModelId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal Weight { get; private set; }

    public ScoringDirection Direction { get; private set; }

    public NormalizationMethod NormalizationMethod { get; private set; }

    public string DataSource { get; private set; } = string.Empty;

    public string Unit { get; private set; } = string.Empty;

    public decimal LowerThreshold { get; private set; }

    public decimal UpperThreshold { get; private set; }

    public MissingDataBehavior MissingDataBehavior { get; private set; }

    public int SortOrder { get; private set; }

    public static ScoringCriterion Create(
        string key,
        string name,
        decimal weight,
        ScoringDirection direction,
        NormalizationMethod normalizationMethod,
        string dataSource,
        string unit,
        decimal lowerThreshold,
        decimal upperThreshold,
        MissingDataBehavior missingDataBehavior,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "A criterion key is required.",
                nameof(key));
        }

        if (key.Trim().Length > 100)
        {
            throw new ArgumentException(
                "A criterion key cannot exceed 100 characters.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "A criterion name is required.",
                nameof(name));
        }

        if (name.Trim().Length > 200)
        {
            throw new ArgumentException(
                "A criterion name cannot exceed 200 characters.",
                nameof(name));
        }

        if (weight <= 0 || weight > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight),
                "Criterion weight must be greater than 0 and no more than 100.");
        }

        if (lowerThreshold >= upperThreshold)
        {
            throw new ArgumentException(
                "The upper threshold must be greater than the lower threshold.",
                nameof(upperThreshold));
        }

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new ArgumentException(
                "A criterion data source is required.",
                nameof(dataSource));
        }

        if (dataSource.Trim().Length > 300)
        {
            throw new ArgumentException(
                "A criterion data source cannot exceed 300 characters.",
                nameof(dataSource));
        }

        var normalizedUnit = unit?.Trim() ?? string.Empty;
        if (normalizedUnit.Length > 50)
        {
            throw new ArgumentException(
                "A criterion unit cannot exceed 50 characters.",
                nameof(unit));
        }

        return new ScoringCriterion(
            Guid.NewGuid(),
            key.Trim(),
            name.Trim(),
            decimal.Round(weight, 4),
            direction,
            normalizationMethod,
            dataSource.Trim(),
            normalizedUnit,
            decimal.Round(lowerThreshold, 4),
            decimal.Round(upperThreshold, 4),
            missingDataBehavior,
            sortOrder);
    }
}
