using FormaGeo.Application.Sites.Contracts;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace FormaGeo.Application.Sites.Mapping;

public static class GeoJsonPolygonMapper
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(
            srid: 4326);

    public static Polygon ToDomain(
        GeoJsonPolygonRequest? request)
    {
        if (request is null)
        {
            throw new ArgumentException(
                "A site boundary is required.");
        }

        if (!string.Equals(
                request.Type,
                "Polygon",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The boundary type must be Polygon.");
        }

        if (request.Coordinates is null ||
            request.Coordinates.Length == 0)
        {
            throw new ArgumentException(
                "The polygon must contain at least one ring.");
        }

        var exteriorRing = CreateLinearRing(
            request.Coordinates[0],
            "exterior");

        var interiorRings = request.Coordinates
            .Skip(1)
            .Select((coordinates, index) =>
                CreateLinearRing(
                    coordinates,
                    $"interior ring {index + 1}"))
            .ToArray();

        var polygon = GeometryFactory.CreatePolygon(
            exteriorRing,
            interiorRings);

        polygon.SRID = 4326;

        if (polygon.IsEmpty)
        {
            throw new ArgumentException(
                "The polygon cannot be empty.");
        }

        if (!polygon.IsValid)
        {
            throw new ArgumentException(
                "The polygon geometry is invalid. " +
                "Check for self-intersections or invalid rings.");
        }

        return polygon;
    }

    public static GeoJsonPolygonResponse ToResponse(
        Polygon polygon)
    {
        var rings = new List<double[][]>
        {
            ToPositions(polygon.ExteriorRing.Coordinates)
        };

        for (var index = 0;
             index < polygon.NumInteriorRings;
             index++)
        {
            var interiorRing =
                polygon.GetInteriorRingN(index);

            rings.Add(
                ToPositions(interiorRing.Coordinates));
        }

        return new GeoJsonPolygonResponse(
            Type: "Polygon",
            Coordinates: rings.ToArray());
    }

    private static LinearRing CreateLinearRing(
        double[][]? positions,
        string ringName)
    {
        if (positions is null || positions.Length < 4)
        {
            throw new ArgumentException(
                $"The {ringName} must contain at least " +
                "four coordinate positions.");
        }

        var coordinates = positions
            .Select((position, index) =>
                CreateCoordinate(
                    position,
                    ringName,
                    index))
            .ToArray();

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            throw new ArgumentException(
                $"The {ringName} must be closed. " +
                "Its first and last positions must match.");
        }

        return GeometryFactory.CreateLinearRing(
            coordinates);
    }

    private static Coordinate CreateCoordinate(
        double[]? position,
        string ringName,
        int positionIndex)
    {
        if (position is null || position.Length != 2)
        {
            throw new ArgumentException(
                $"Position {positionIndex} in the {ringName} " +
                "must contain exactly two numbers: " +
                "[longitude, latitude].");
        }

        var longitude = position[0];
        var latitude = position[1];

        if (!double.IsFinite(longitude) ||
            !double.IsFinite(latitude))
        {
            throw new ArgumentException(
                $"Position {positionIndex} in the {ringName} " +
                "contains a non-finite coordinate.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentException(
                $"Longitude at position {positionIndex} " +
                $"in the {ringName} must be between -180 and 180.");
        }

        if (latitude is < -90 or > 90)
        {
            throw new ArgumentException(
                $"Latitude at position {positionIndex} " +
                $"in the {ringName} must be between -90 and 90.");
        }

        return new Coordinate(
            x: longitude,
            y: latitude);
    }

    private static double[][] ToPositions(
        Coordinate[] coordinates)
    {
        return coordinates
            .Select(coordinate => new[]
            {
                coordinate.X,
                coordinate.Y
            })
            .ToArray();
    }
}