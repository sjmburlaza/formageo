using FormaGeo.Infrastructure.Persistence.Seeding;

namespace FormaGeo.IntegrationTests.Persistence;

public sealed class DevelopmentMockDataTests
{
    [Fact]
    public void CreateSites_ReturnsDistinctAnalysisReadySites()
    {
        var projectId = Guid.NewGuid();

        var sites =
            DevelopmentMockData.CreateSites(projectId);

        Assert.Equal(5, sites.Count);
        Assert.Equal(
            sites.Count,
            sites.Select(site => site.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            sites,
            site =>
            {
                Assert.Equal(projectId, site.ProjectId);
                Assert.Equal(4326, site.Boundary.SRID);
                Assert.True(site.Boundary.IsValid);
                Assert.False(site.Boundary.IsEmpty);
                Assert.True(site.Boundary.Area > 0);
                Assert.InRange(
                    site.Boundary.Centroid.X,
                    120.98,
                    121.05);
                Assert.InRange(
                    site.Boundary.Centroid.Y,
                    14.54,
                    14.61);
            });
    }
}
