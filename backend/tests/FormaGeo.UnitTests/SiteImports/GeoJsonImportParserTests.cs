using FormaGeo.Application.SiteImports;

namespace FormaGeo.UnitTests.SiteImports;

public sealed class GeoJsonImportParserTests
{
    [Fact]
    public void Parse_ReadsPolygonFeatureMetadata()
    {
        var parsed = GeoJsonImportParser.Parse(
            """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "properties": { "name": "North parcel" },
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [
                      [
                        [121.0, 14.0],
                        [121.1, 14.0],
                        [121.1, 14.1],
                        [121.0, 14.0]
                      ]
                    ]
                  }
                }
              ]
            }
            """,
            "sites");

        var feature = Assert.Single(parsed.Features);
        Assert.Equal("North parcel", feature.Name);
        Assert.Null(feature.Error);
        Assert.Equal(4326, Assert.Single(feature.Polygons).SRID);
        Assert.Equal("WGS 84 (EPSG:4326)", parsed.DetectedCoordinateSystem);
    }

    [Fact]
    public void Parse_TransformsDeclaredWebMercatorCoordinates()
    {
        var parsed = GeoJsonImportParser.Parse(
            """
            {
              "type": "Polygon",
              "crs": {
                "type": "name",
                "properties": { "name": "EPSG:3857" }
              },
              "coordinates": [
                [
                  [0.0, 0.0],
                  [111319.490793, 0.0],
                  [111319.490793, 111325.142866],
                  [0.0, 0.0]
                ]
              ]
            }
            """,
            "mercator");

        var polygon = Assert.Single(
            Assert.Single(parsed.Features).Polygons);

        Assert.Equal(1, polygon.Coordinates[1].X, precision: 5);
        Assert.Equal(1, polygon.Coordinates[2].Y, precision: 5);
        Assert.Contains(
            "transformed",
            parsed.DetectedCoordinateSystem,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ReportsUnsupportedGeometryAsFeatureError()
    {
        var parsed = GeoJsonImportParser.Parse(
            """
            {
              "type": "Feature",
              "properties": { "name": "Survey point" },
              "geometry": {
                "type": "Point",
                "coordinates": [121.0, 14.0]
              }
            }
            """,
            "points");

        var feature = Assert.Single(parsed.Features);
        Assert.Empty(feature.Polygons);
        Assert.Contains(
            "not supported",
            feature.Error,
            StringComparison.OrdinalIgnoreCase);
    }
}
