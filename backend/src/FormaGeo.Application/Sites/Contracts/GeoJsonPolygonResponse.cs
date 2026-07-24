namespace FormaGeo.Application.Sites.Contracts;

public sealed record GeoJsonPolygonResponse(
    string Type,
    double[][][] Coordinates);