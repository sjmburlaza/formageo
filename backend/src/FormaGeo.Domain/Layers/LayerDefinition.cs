namespace FormaGeo.Domain.Layers;

public sealed class LayerDefinition
{
    private LayerDefinition()
    {
        // Required by Entity Framework Core.
    }

    public Guid Id { get; private set; }

    public Guid DataSourceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public LayerCategory Category { get; private set; }

    public string GeographicCoverage { get; private set; } = string.Empty;

    public string CoordinateSystem { get; private set; } = string.Empty;

    public LayerGeometryType GeometryType { get; private set; }

    public string FeatureNameProperty { get; private set; } = "name";

    public string StyleJson { get; private set; } = "{}";

    public bool IsActive { get; private set; }

    public DataSource DataSource { get; private set; } = null!;

    public ICollection<LayerVersion> Versions { get; private set; } = [];

    public ICollection<LayerLegend> LegendItems { get; private set; } = [];
}
