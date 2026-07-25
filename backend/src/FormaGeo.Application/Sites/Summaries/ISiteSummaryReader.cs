namespace FormaGeo.Application.Sites.Summaries;

public interface ISiteSummaryReader
{
    Task<SiteSpatialSummary?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);
}

public sealed record SiteSpatialSummary(
    string GeometryType,
    int Srid,
    bool IsValid,
    string ValidityReason,
    int RingCount,
    int VertexCount,
    double? AreaSquareMetres,
    double? PerimeterMetres,
    double? CentroidLongitude,
    double? CentroidLatitude,
    double BoundingBoxWest,
    double BoundingBoxSouth,
    double BoundingBoxEast,
    double BoundingBoxNorth);
