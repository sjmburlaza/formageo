using FormaGeo.Domain.Sites;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace FormaGeo.UnitTests.Sites;

public sealed class SiteLifecycleTests
{
    [Fact]
    public void Create_InitializesActiveLifecycleState()
    {
        var site = CreateSite();

        Assert.Equal(SiteStatus.Active, site.Status);
        Assert.Equal(site.CreatedAtUtc, site.UpdatedAtUtc);
        Assert.Null(site.ArchivedAtUtc);
    }

    [Fact]
    public void Rename_NormalizesNameAndUpdatesTimestamp()
    {
        var site = CreateSite();
        var previousUpdatedAt = site.UpdatedAtUtc;

        site.Rename("  Central candidate  ");

        Assert.Equal("Central candidate", site.Name);
        Assert.True(site.UpdatedAtUtc >= previousUpdatedAt);
    }

    [Fact]
    public void UpdateBoundary_ReplacesGeometryWithoutChangingIdentity()
    {
        var site = CreateSite();
        var replacement = Polygon(
            [121.2, 14.2],
            [121.3, 14.2],
            [121.3, 14.3],
            [121.2, 14.2]);

        site.UpdateBoundary(replacement);

        Assert.Same(replacement, site.Boundary);
        Assert.Equal(4326, site.Boundary.SRID);
    }

    [Fact]
    public void ArchiveAndRestore_TrackLifecycleState()
    {
        var site = CreateSite();

        site.Archive();

        Assert.Equal(SiteStatus.Archived, site.Status);
        Assert.NotNull(site.ArchivedAtUtc);

        site.Restore();

        Assert.Equal(SiteStatus.Active, site.Status);
        Assert.Null(site.ArchivedAtUtc);
    }

    private static Site CreateSite()
    {
        return Site.Create(
            Guid.NewGuid(),
            "North candidate",
            Polygon(
                [121.0, 14.0],
                [121.1, 14.0],
                [121.1, 14.1],
                [121.0, 14.0]));
    }

    private static Polygon Polygon(params double[][] positions)
    {
        var geometryFactory =
            NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        var coordinates = positions
            .Select(position =>
                new Coordinate(position[0], position[1]))
            .ToArray();

        return geometryFactory.CreatePolygon(coordinates);
    }
}
