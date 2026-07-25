using FormaGeo.Application.Sites.Contracts;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Valid;

namespace FormaGeo.Application.Sites.Mapping;

public static class GeoJsonPolygonMapper
{
    public const int RequiredSrid = 4326;
    public const int MaximumCoordinateCount = 10_000;

    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(
            srid: RequiredSrid);

    public static Polygon ToDomain(
        GeoJsonPolygonRequest? request)
    {
        if (request is null)
        {
            throw Invalid(
                "geometry_required",
                "A site boundary is required.");
        }

        if (!string.Equals(
                request.Type,
                "Polygon",
                StringComparison.Ordinal))
        {
            throw Invalid(
                "geometry_type",
                "The boundary type must be Polygon.");
        }

        if (request.Srid.HasValue &&
            request.Srid.Value != RequiredSrid)
        {
            throw Invalid(
                "invalid_srid",
                $"The boundary SRID must be {RequiredSrid} (WGS 84).");
        }

        if (request.Coordinates is null ||
            request.Coordinates.Length == 0)
        {
            throw Invalid(
                "empty_geometry",
                "The polygon cannot be empty and must contain at least one ring.");
        }

        var coordinateCount = request.Coordinates.Sum(
            ring => ring?.Length ?? 0);

        if (coordinateCount > MaximumCoordinateCount)
        {
            throw Invalid(
                "coordinate_limit",
                $"The polygon contains {coordinateCount} coordinate positions. " +
                $"The maximum is {MaximumCoordinateCount}.");
        }

        var exteriorRing = CreateLinearRing(
            request.Coordinates[0],
            "exterior ring");

        var interiorRings = request.Coordinates
            .Skip(1)
            .Select((coordinates, index) =>
                CreateLinearRing(
                    coordinates,
                    $"interior ring {index + 1}"))
            .ToArray();

        Polygon polygon;

        try
        {
            polygon = GeometryFactory.CreatePolygon(
                exteriorRing,
                interiorRings);
        }
        catch (ArgumentException exception)
        {
            throw Invalid(
                "invalid_geometry",
                $"The polygon rings are invalid: {exception.Message}");
        }

        polygon.SRID = RequiredSrid;

        if (polygon.IsEmpty)
        {
            throw Invalid(
                "empty_geometry",
                "The polygon cannot be empty.");
        }

        var validityCheck = new IsValidOp(polygon);

        if (!validityCheck.IsValid)
        {
            var validationMessage =
                validityCheck.ValidationError?.Message ??
                "The rings do not form a valid polygon.";
            var problem = validationMessage.Contains(
                "Self-intersection",
                StringComparison.OrdinalIgnoreCase)
                ? "self_intersection"
                : "invalid_geometry";

            throw Invalid(
                problem,
                $"The polygon geometry is invalid: {validationMessage}.");
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
        if (positions is null || positions.Length == 0)
        {
            throw Invalid(
                "empty_ring",
                $"The {ringName} cannot be empty.");
        }

        if (positions.Length < 4)
        {
            throw Invalid(
                "ring_too_short",
                $"The {ringName} must contain at least four coordinate positions.");
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
            throw Invalid(
                "unclosed_ring",
                $"The {ringName} must be closed. " +
                "Its first and last positions must match.");
        }

        try
        {
            return GeometryFactory.CreateLinearRing(
                coordinates);
        }
        catch (ArgumentException exception)
        {
            throw Invalid(
                "invalid_ring",
                $"The {ringName} is invalid: {exception.Message}");
        }
    }

    private static Coordinate CreateCoordinate(
        double[]? position,
        string ringName,
        int positionIndex)
    {
        if (position is null || position.Length != 2)
        {
            throw Invalid(
                "invalid_position",
                $"Position {positionIndex} in the {ringName} " +
                "must contain exactly two numbers: [longitude, latitude].");
        }

        var longitude = position[0];
        var latitude = position[1];

        if (!double.IsFinite(longitude) ||
            !double.IsFinite(latitude))
        {
            throw Invalid(
                "invalid_coordinate",
                $"Position {positionIndex} in the {ringName} " +
                "contains a non-finite coordinate.");
        }

        if (longitude is < -180 or > 180)
        {
            throw Invalid(
                "invalid_longitude",
                $"Longitude at position {positionIndex} " +
                $"in the {ringName} must be between -180 and 180.");
        }

        if (latitude is < -90 or > 90)
        {
            throw Invalid(
                "invalid_latitude",
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

    private static PolygonValidationException Invalid(
        string problem,
        string message)
    {
        return new PolygonValidationException(
            problem,
            message);
    }
}

public sealed class PolygonValidationException : ArgumentException
{
    public PolygonValidationException(
        string problem,
        string message)
        : base(message)
    {
        Problem = problem;
    }

    public string Problem { get; }
}
