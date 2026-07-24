using NetTopologySuite.Geometries;

namespace FormaGeo.Domain.Sites;

public sealed class Site
{
    private Site()
    {
        // Required by Entity Framework Core.
    }

    private Site(
        Guid id,
        Guid projectId,
        string name,
        Polygon boundary,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        Name = name;
        Boundary = boundary;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Polygon Boundary { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Site Create(
        Guid projectId,
        string name,
        Polygon boundary)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "A project ID is required.",
                nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Site name is required.",
                nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length > 200)
        {
            throw new ArgumentException(
                "Site name cannot exceed 200 characters.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(boundary);

        if (boundary.IsEmpty)
        {
            throw new ArgumentException(
                "The site boundary cannot be empty.",
                nameof(boundary));
        }

        if (!boundary.IsValid)
        {
            throw new ArgumentException(
                "The site boundary is invalid.",
                nameof(boundary));
        }

        boundary.SRID = 4326;

        return new Site(
            Guid.NewGuid(),
            projectId,
            normalizedName,
            boundary,
            DateTimeOffset.UtcNow);
    }
}