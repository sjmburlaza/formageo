using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.Mapping;

namespace FormaGeo.UnitTests.Sites;

public sealed class GeoJsonPolygonMapperTests
{
    [Fact]
    public void ToDomain_ReturnsPolygonWithSrid4326()
    {
        var request = Polygon(
            [121.0, 14.0],
            [121.1, 14.0],
            [121.1, 14.1],
            [121.0, 14.0]);

        var polygon = GeoJsonPolygonMapper.ToDomain(request);

        Assert.Equal(4326, polygon.SRID);
        Assert.True(polygon.IsValid);
    }

    [Fact]
    public void ToDomain_RejectsNonPolygonGeometry()
    {
        var request = new GeoJsonPolygonRequest(
            "MultiPolygon",
            [[[121.0, 14.0], [121.1, 14.0], [121.1, 14.1], [121.0, 14.0]]]);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("geometry_type", exception.Problem);
    }

    [Fact]
    public void ToDomain_RejectsEmptyGeometry()
    {
        var request = new GeoJsonPolygonRequest(
            "Polygon",
            []);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("empty_geometry", exception.Problem);
    }

    [Fact]
    public void ToDomain_RejectsUnclosedRing()
    {
        var request = Polygon(
            [121.0, 14.0],
            [121.1, 14.0],
            [121.1, 14.1],
            [121.0, 14.1]);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("unclosed_ring", exception.Problem);
    }

    [Theory]
    [InlineData(181, 14, "invalid_longitude")]
    [InlineData(121, 91, "invalid_latitude")]
    public void ToDomain_RejectsOutOfRangeCoordinates(
        double longitude,
        double latitude,
        string expectedProblem)
    {
        var request = Polygon(
            [longitude, latitude],
            [121.1, 14.0],
            [121.1, 14.1],
            [longitude, latitude]);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal(expectedProblem, exception.Problem);
    }

    [Fact]
    public void ToDomain_RejectsSelfIntersectingPolygon()
    {
        var request = Polygon(
            [121.0, 14.0],
            [121.1, 14.1],
            [121.1, 14.0],
            [121.0, 14.1],
            [121.0, 14.0]);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("self_intersection", exception.Problem);
        Assert.Contains(
            "invalid",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToDomain_RejectsNonWgs84Srid()
    {
        var request = Polygon(
            [121.0, 14.0],
            [121.1, 14.0],
            [121.1, 14.1],
            [121.0, 14.0]) with
        {
            Srid = 3857
        };

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("invalid_srid", exception.Problem);
    }

    [Fact]
    public void ToDomain_RejectsCoordinateCountOverLimit()
    {
        var positions = Enumerable
            .Repeat(
                new[] { 121.0, 14.0 },
                GeoJsonPolygonMapper.MaximumCoordinateCount + 1)
            .ToArray();
        var request = new GeoJsonPolygonRequest(
            "Polygon",
            [positions]);

        var exception = Assert.Throws<PolygonValidationException>(
            () => GeoJsonPolygonMapper.ToDomain(request));

        Assert.Equal("coordinate_limit", exception.Problem);
    }

    private static GeoJsonPolygonRequest Polygon(
        params double[][] positions)
    {
        return new GeoJsonPolygonRequest(
            "Polygon",
            [positions]);
    }
}
