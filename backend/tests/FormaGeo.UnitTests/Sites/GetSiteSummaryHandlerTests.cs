using FormaGeo.Application.Sites.GetSiteSummary;
using FormaGeo.Application.Sites.Summaries;

namespace FormaGeo.UnitTests.Sites;

public sealed class GetSiteSummaryHandlerTests
{
    [Fact]
    public async Task HandleAsync_MapsMetricMeasurementsAndLocation()
    {
        var siteId = Guid.NewGuid();
        var reader = new StubSiteSummaryReader(
            new SiteSpatialSummary(
                "ST_Polygon",
                4326,
                true,
                "Valid Geometry",
                1,
                4,
                18_400,
                612,
                121.031,
                14.6507,
                121.029,
                14.649,
                121.033,
                14.652));
        var handler = new GetSiteSummaryHandler(reader);

        var result = await handler.HandleAsync(siteId);

        Assert.NotNull(result);
        Assert.Equal("Polygon", result.Geometry.Type);
        Assert.Equal("WGS 84 (EPSG:4326)", result.Geometry.CoordinateSystem);
        Assert.Equal(18_400, result.Measurements.AreaSquareMetres);
        Assert.Equal(1.84, result.Measurements.AreaHectares);
        Assert.Equal(612, result.Measurements.PerimeterMetres);
        Assert.Equal(121.031, result.Location.Centroid?.Longitude);
        Assert.Empty(result.DataQualityWarnings);
    }

    [Fact]
    public async Task HandleAsync_ExplainsInvalidGeometry()
    {
        var reader = new StubSiteSummaryReader(
            new SiteSpatialSummary(
                "ST_Polygon",
                4326,
                false,
                "Self-intersection",
                1,
                4,
                null,
                null,
                null,
                null,
                121,
                14,
                121.1,
                14.1));
        var handler = new GetSiteSummaryHandler(reader);

        var result =
            await handler.HandleAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.False(result.Geometry.IsValid);
        Assert.Null(result.Measurements.AreaSquareMetres);
        Assert.Null(result.Location.Centroid);
        Assert.Contains(
            result.DataQualityWarnings,
            warning => warning.Contains(
                "Self-intersection",
                StringComparison.Ordinal));
    }

    private sealed class StubSiteSummaryReader
        : ISiteSummaryReader
    {
        private readonly SiteSpatialSummary? _summary;

        public StubSiteSummaryReader(
            SiteSpatialSummary? summary)
        {
            _summary = summary;
        }

        public Task<SiteSpatialSummary?> GetAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_summary);
        }
    }
}
