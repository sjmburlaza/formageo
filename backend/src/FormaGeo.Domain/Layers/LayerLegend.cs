namespace FormaGeo.Domain.Layers;

public sealed class LayerLegend
{
    private LayerLegend()
    {
        // Required by Entity Framework Core.
    }

    public Guid Id { get; private set; }

    public Guid LayerDefinitionId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string FillColor { get; private set; } = string.Empty;

    public string StrokeColor { get; private set; } = string.Empty;

    public string? Symbol { get; private set; }

    public int SortOrder { get; private set; }
}
