namespace FormaGeo.Domain.Layers;

public sealed class DataSource
{
    private DataSource()
    {
        // Required by Entity Framework Core.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Organization { get; private set; } = string.Empty;

    public string LicenseName { get; private set; } = string.Empty;

    public string? LicenseUrl { get; private set; }

    public string Attribution { get; private set; } = string.Empty;

    public string? SourceUrl { get; private set; }
}
