namespace FormaGeo.Application.Sites.Contracts;

public sealed record SiteSummaryResponse(
    Guid SiteId,
    SiteGeometrySummaryResponse Geometry,
    SiteMeasurementsSummaryResponse Measurements,
    SiteLocationSummaryResponse Location,
    IReadOnlyList<string> DataQualityWarnings);

public sealed record SiteGeometrySummaryResponse(
    string Type,
    int Srid,
    string CoordinateSystem,
    bool IsValid,
    string ValidityReason,
    int RingCount,
    int VertexCount);

public sealed record SiteMeasurementsSummaryResponse(
    double? AreaSquareMetres,
    double? AreaHectares,
    double? PerimeterMetres,
    string CalculationMethod);

public sealed record SiteLocationSummaryResponse(
    SiteCoordinateResponse? Centroid,
    SiteBoundingBoxResponse BoundingBox);

public sealed record SiteCoordinateResponse(
    double Longitude,
    double Latitude);

public sealed record SiteBoundingBoxResponse(
    double West,
    double South,
    double East,
    double North);
