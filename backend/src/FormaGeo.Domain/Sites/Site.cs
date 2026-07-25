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
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        SiteStatus status)
    {
        Id = id;
        ProjectId = projectId;
        Name = name;
        Boundary = boundary;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        Status = status;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Polygon Boundary { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public SiteStatus Status { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

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

        var normalizedName = NormalizeName(name);
        ValidateBoundary(boundary);
        var now = DateTimeOffset.UtcNow;

        return new Site(
            Guid.NewGuid(),
            projectId,
            normalizedName,
            boundary,
            now,
            now,
            SiteStatus.Active);
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void UpdateBoundary(Polygon boundary)
    {
        ValidateBoundary(boundary);
        Boundary = boundary;
        Touch();
    }

    public void Archive()
    {
        if (Status == SiteStatus.Archived)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        Status = SiteStatus.Archived;
        ArchivedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void Restore()
    {
        if (Status != SiteStatus.Archived)
        {
            return;
        }

        Status = SiteStatus.Active;
        ArchivedAtUtc = null;
        Touch();
    }

    private static string NormalizeName(string name)
    {
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

        return normalizedName;
    }

    private static void ValidateBoundary(Polygon boundary)
    {
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
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
