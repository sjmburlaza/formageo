using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.Summaries;

namespace FormaGeo.Application.Sites.GetSiteSummary;

public sealed class GetSiteSummaryHandler
{
    private const int ComplexBoundaryVertexThreshold = 5_000;

    private readonly ISiteSummaryReader _siteSummaryReader;

    public GetSiteSummaryHandler(
        ISiteSummaryReader siteSummaryReader)
    {
        _siteSummaryReader = siteSummaryReader;
    }

    public async Task<SiteSummaryResponse?> HandleAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var summary = await _siteSummaryReader.GetAsync(
            siteId,
            cancellationToken);

        if (summary is null)
        {
            return null;
        }

        var centroid =
            summary.CentroidLongitude.HasValue &&
            summary.CentroidLatitude.HasValue
                ? new SiteCoordinateResponse(
                    summary.CentroidLongitude.Value,
                    summary.CentroidLatitude.Value)
                : null;

        return new SiteSummaryResponse(
            siteId,
            new SiteGeometrySummaryResponse(
                NormalizeGeometryType(summary.GeometryType),
                summary.Srid,
                summary.Srid == 4326
                    ? "WGS 84 (EPSG:4326)"
                    : $"EPSG:{summary.Srid}",
                summary.IsValid,
                summary.ValidityReason,
                summary.RingCount,
                summary.VertexCount),
            new SiteMeasurementsSummaryResponse(
                summary.AreaSquareMetres,
                summary.AreaSquareMetres / 10_000d,
                summary.PerimeterMetres,
                "PostGIS geography on the WGS 84 spheroid"),
            new SiteLocationSummaryResponse(
                centroid,
                new SiteBoundingBoxResponse(
                    summary.BoundingBoxWest,
                    summary.BoundingBoxSouth,
                    summary.BoundingBoxEast,
                    summary.BoundingBoxNorth)),
            BuildWarnings(summary));
    }

    private static IReadOnlyList<string> BuildWarnings(
        SiteSpatialSummary summary)
    {
        var warnings = new List<string>();

        if (!summary.IsValid)
        {
            warnings.Add(
                $"Boundary geometry is invalid: {summary.ValidityReason}. " +
                "Area, perimeter, and centroid are unavailable until the boundary is corrected.");
        }

        if (summary.VertexCount >= ComplexBoundaryVertexThreshold)
        {
            warnings.Add(
                $"The boundary contains {summary.VertexCount:N0} vertices and may be slow to display or edit.");
        }

        if (summary.BoundingBoxEast - summary.BoundingBoxWest > 180d)
        {
            warnings.Add(
                "The boundary spans more than 180° of longitude. " +
                "Review it for an antimeridian crossing; the bounding box uses native WGS 84 longitudes.");
        }

        return warnings;
    }

    private static string NormalizeGeometryType(
        string geometryType)
    {
        return geometryType.StartsWith(
            "ST_",
            StringComparison.OrdinalIgnoreCase)
            ? geometryType[3..]
            : geometryType;
    }
}
