using System.Text.Json;
using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.Mapping;
using NetTopologySuite.Geometries;

namespace FormaGeo.Application.SiteImports;

public sealed record ParsedGeoJsonImport(
    string DetectedCoordinateSystem,
    IReadOnlyList<ParsedGeoJsonFeature> Features);

public sealed record ParsedGeoJsonFeature(
    int Index,
    string Name,
    IReadOnlyList<Polygon> Polygons,
    string? Error,
    IReadOnlyList<string> Warnings);

public static class GeoJsonImportParser
{
    private const double EarthRadiusMetres = 6_378_137;

    public static ParsedGeoJsonImport Parse(
        string content,
        string fallbackName)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException exception)
        {
            throw Invalid(
                "content",
                "invalid_geojson",
                $"The file is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(
                    "content",
                    "invalid_geojson",
                    "The GeoJSON root must be an object.");
            }

            var sourceSrid = DetectSrid(root);
            var coordinateSystem = sourceSrid == 3857
                ? "Web Mercator (EPSG:3857), transformed to WGS 84"
                : "WGS 84 (EPSG:4326)";
            var features = ReadFeatures(
                root,
                fallbackName,
                sourceSrid);

            return new ParsedGeoJsonImport(
                coordinateSystem,
                features);
        }
    }

    private static IReadOnlyList<ParsedGeoJsonFeature> ReadFeatures(
        JsonElement root,
        string fallbackName,
        int sourceSrid)
    {
        var type = ReadRequiredString(root, "type");

        if (string.Equals(
            type,
            "FeatureCollection",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!root.TryGetProperty("features", out var featuresElement) ||
                featuresElement.ValueKind != JsonValueKind.Array)
            {
                throw Invalid(
                    "content",
                    "invalid_feature_collection",
                    "A FeatureCollection must contain a features array.");
            }

            return featuresElement
                .EnumerateArray()
                .Select((feature, index) =>
                    ReadFeature(
                        feature,
                        index,
                        fallbackName,
                        sourceSrid))
                .ToArray();
        }

        if (string.Equals(
            type,
            "Feature",
            StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                ReadFeature(
                    root,
                    0,
                    fallbackName,
                    sourceSrid)
            ];
        }

        return
        [
            ReadGeometryFeature(
                root,
                0,
                fallbackName,
                sourceSrid)
        ];
    }

    private static ParsedGeoJsonFeature ReadFeature(
        JsonElement feature,
        int index,
        string fallbackName,
        int sourceSrid)
    {
        if (feature.ValueKind != JsonValueKind.Object)
        {
            return Failed(
                index,
                $"{fallbackName} {index + 1}",
                "The feature must be a GeoJSON object.");
        }

        var name = ReadFeatureName(
            feature,
            $"{fallbackName} {index + 1}");

        if (!feature.TryGetProperty("geometry", out var geometry) ||
            geometry.ValueKind is JsonValueKind.Null or
                JsonValueKind.Undefined)
        {
            return Failed(
                index,
                name,
                "The feature does not contain a geometry.");
        }

        return ReadGeometryFeature(
            geometry,
            index,
            name,
            sourceSrid);
    }

    private static ParsedGeoJsonFeature ReadGeometryFeature(
        JsonElement geometry,
        int index,
        string name,
        int sourceSrid)
    {
        try
        {
            var geometryType =
                ReadRequiredString(geometry, "type");

            if (!geometry.TryGetProperty(
                "coordinates",
                out var coordinates))
            {
                return Failed(
                    index,
                    name,
                    "The geometry does not contain coordinates.");
            }

            return geometryType switch
            {
                "Polygon" => ReadPolygon(
                    coordinates,
                    index,
                    name,
                    sourceSrid),
                "MultiPolygon" => ReadMultiPolygon(
                    coordinates,
                    index,
                    name,
                    sourceSrid),
                _ => Failed(
                    index,
                    name,
                    $"Geometry type '{geometryType}' is not supported. " +
                    "Use Polygon or MultiPolygon.")
            };
        }
        catch (SiteImportException exception)
        {
            return Failed(index, name, exception.Message);
        }
        catch (JsonException exception)
        {
            return Failed(
                index,
                name,
                $"The geometry coordinates are malformed: {exception.Message}");
        }
    }

    private static ParsedGeoJsonFeature ReadPolygon(
        JsonElement coordinates,
        int index,
        string name,
        int sourceSrid)
    {
        var rings =
            coordinates.Deserialize<double[][][]>()
            ?? throw Invalid(
                "content",
                "invalid_coordinates",
                "The Polygon coordinates cannot be null.");

        var warnings = new List<string>();
        var polygons = BuildPolygons(
            TransformIfRequired(rings, sourceSrid),
            warnings);

        return new ParsedGeoJsonFeature(
            index,
            name,
            polygons,
            null,
            warnings);
    }

    private static ParsedGeoJsonFeature ReadMultiPolygon(
        JsonElement coordinates,
        int index,
        string name,
        int sourceSrid)
    {
        var polygonCoordinates =
            coordinates.Deserialize<double[][][][]>()
            ?? throw Invalid(
                "content",
                "invalid_coordinates",
                "The MultiPolygon coordinates cannot be null.");
        var warnings = new List<string>();
        var polygons = polygonCoordinates
            .SelectMany(rings => BuildPolygons(
                TransformIfRequired(rings, sourceSrid),
                warnings))
            .ToArray();

        if (polygons.Length == 0)
        {
            return Failed(
                index,
                name,
                "The MultiPolygon does not contain any polygons.");
        }

        return new ParsedGeoJsonFeature(
            index,
            name,
            polygons,
            null,
            warnings);
    }

    private static IReadOnlyList<Polygon> BuildPolygons(
        double[][][] rings,
        List<string> warnings)
    {
        try
        {
            var polygons =
                GeoJsonPolygonMapper.ToImportPolygons(
                    new GeoJsonPolygonRequest(
                        "Polygon",
                        rings,
                        GeoJsonPolygonMapper.RequiredSrid),
                    out var repaired);

            if (repaired)
            {
                warnings.Add(
                    "An invalid polygon was repaired during import.");
            }

            return polygons;
        }
        catch (PolygonValidationException exception)
        {
            throw Invalid(
                "content",
                exception.Problem,
                exception.Message);
        }
    }

    private static double[][][] TransformIfRequired(
        double[][][] rings,
        int sourceSrid)
    {
        if (sourceSrid != 3857)
        {
            return rings;
        }

        return rings
            .Select(ring => ring
                .Select(position =>
                    TransformWebMercatorPosition(position))
                .ToArray())
            .ToArray();
    }

    private static double[] TransformWebMercatorPosition(
        double[] position)
    {
        if (position.Length != 2 ||
            !double.IsFinite(position[0]) ||
            !double.IsFinite(position[1]))
        {
            return position;
        }

        var longitude =
            position[0] / EarthRadiusMetres * 180 / Math.PI;
        var latitude =
            (2 * Math.Atan(
                Math.Exp(position[1] / EarthRadiusMetres)) -
             Math.PI / 2) *
            180 / Math.PI;

        return [longitude, latitude];
    }

    private static int DetectSrid(JsonElement root)
    {
        if (!root.TryGetProperty("crs", out var crs) ||
            crs.ValueKind is JsonValueKind.Null or
                JsonValueKind.Undefined)
        {
            return GeoJsonPolygonMapper.RequiredSrid;
        }

        string? name = null;

        if (crs.ValueKind == JsonValueKind.Object &&
            crs.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object &&
            properties.TryGetProperty("name", out var nameElement) &&
            nameElement.ValueKind == JsonValueKind.String)
        {
            name = nameElement.GetString();
        }

        var normalized = name?.ToUpperInvariant() ?? string.Empty;

        if (normalized.Contains("3857", StringComparison.Ordinal) ||
            normalized.Contains("900913", StringComparison.Ordinal))
        {
            return 3857;
        }

        if (normalized.Contains("4326", StringComparison.Ordinal) ||
            normalized.Contains("CRS84", StringComparison.Ordinal))
        {
            return GeoJsonPolygonMapper.RequiredSrid;
        }

        throw Invalid(
            "content",
            "unsupported_crs",
            string.IsNullOrWhiteSpace(name)
                ? "The GeoJSON coordinate system could not be detected."
                : $"Coordinate system '{name}' is not supported. " +
                  "Use EPSG:4326 or EPSG:3857.");
    }

    private static string ReadFeatureName(
        JsonElement feature,
        string fallback)
    {
        if (!feature.TryGetProperty(
            "properties",
            out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        foreach (var propertyName in new[] { "name", "title", "id" })
        {
            if (properties.TryGetProperty(
                    propertyName,
                    out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
        }

        return fallback;
    }

    private static string ReadRequiredString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw Invalid(
                "content",
                "invalid_geojson",
                $"GeoJSON '{propertyName}' must be a string.");
        }

        return value.GetString()!;
    }

    private static ParsedGeoJsonFeature Failed(
        int index,
        string name,
        string error)
    {
        return new ParsedGeoJsonFeature(
            index,
            name,
            [],
            error,
            []);
    }

    private static SiteImportException Invalid(
        string field,
        string problem,
        string message)
    {
        return new SiteImportException(
            field,
            problem,
            message);
    }
}
