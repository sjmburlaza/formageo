namespace FormaGeo.Domain.Layers;

public sealed class LayerVersion
{
    private LayerVersion()
    {
        // Required by Entity Framework Core.
    }

    public Guid Id { get; private set; }

    public Guid LayerDefinitionId { get; private set; }

    public string VersionLabel { get; private set; } = string.Empty;

    public DateTimeOffset LastUpdatedAtUtc { get; private set; }

    public LayerDeliveryMethod DeliveryMethod { get; private set; }

    public string DataUrl { get; private set; } = string.Empty;

    public string? SourceLayer { get; private set; }

    public int? MinimumZoom { get; private set; }

    public int? MaximumZoom { get; private set; }

    public bool IsCurrent { get; private set; }
}
