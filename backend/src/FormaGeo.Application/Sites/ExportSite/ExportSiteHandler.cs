using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.ExportSite;

public sealed record GeoJsonSiteProperties(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Status,
    string CoordinateSystem);

public sealed record GeoJsonSiteFeature(
    string Type,
    GeoJsonSiteProperties Properties,
    GeoJsonPolygonResponse Geometry);

public sealed class ExportSiteHandler
{
    private readonly ISiteRepository _siteRepository;

    public ExportSiteHandler(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<GeoJsonSiteFeature?> HandleAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByIdAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            return null;
        }

        return new GeoJsonSiteFeature(
            "Feature",
            new GeoJsonSiteProperties(
                site.Id,
                site.ProjectId,
                site.Name,
                site.Status.ToString(),
                "EPSG:4326"),
            Mapping.GeoJsonPolygonMapper.ToResponse(
                site.Boundary));
    }
}
