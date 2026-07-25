namespace FormaGeo.Domain.Layers;

public sealed class ProjectLayer
{
    private ProjectLayer()
    {
        // Required by Entity Framework Core.
    }

    private ProjectLayer(
        Guid projectId,
        Guid layerDefinitionId,
        bool isVisible,
        decimal opacity,
        int sortOrder,
        string? filter,
        DateTimeOffset updatedAtUtc)
    {
        ProjectId = projectId;
        LayerDefinitionId = layerDefinitionId;
        IsVisible = isVisible;
        Opacity = opacity;
        SortOrder = sortOrder;
        Filter = NormalizeFilter(filter);
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid ProjectId { get; private set; }

    public Guid LayerDefinitionId { get; private set; }

    public bool IsVisible { get; private set; }

    public decimal Opacity { get; private set; }

    public int SortOrder { get; private set; }

    public string? Filter { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static ProjectLayer Create(
        Guid projectId,
        Guid layerDefinitionId,
        bool isVisible,
        decimal opacity,
        int sortOrder,
        string? filter = null)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "A project ID is required.",
                nameof(projectId));
        }

        if (layerDefinitionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A layer ID is required.",
                nameof(layerDefinitionId));
        }

        ValidateOpacity(opacity);
        ValidateSortOrder(sortOrder);

        return new ProjectLayer(
            projectId,
            layerDefinitionId,
            isVisible,
            opacity,
            sortOrder,
            filter,
            DateTimeOffset.UtcNow);
    }

    private static void ValidateOpacity(decimal opacity)
    {
        if (opacity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(opacity),
                "Layer opacity must be between 0 and 1.");
        }
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Layer order cannot be negative.");
        }
    }

    private static string? NormalizeFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var normalizedFilter = filter.Trim();

        if (normalizedFilter.Length > 200)
        {
            throw new ArgumentException(
                "A layer filter cannot exceed 200 characters.",
                nameof(filter));
        }

        return normalizedFilter;
    }
}
