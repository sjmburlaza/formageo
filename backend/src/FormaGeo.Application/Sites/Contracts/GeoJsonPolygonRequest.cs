namespace FormaGeo.Application.Sites.Contracts;

public sealed record GeoJsonPolygonRequest(
    string? Type,
    double[][][]? Coordinates,
    int? Srid = null);
