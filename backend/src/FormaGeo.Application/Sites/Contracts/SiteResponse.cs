using FormaGeo.Application.Sites.Mapping;
using FormaGeo.Domain.Sites;

namespace FormaGeo.Application.Sites.Contracts;

public sealed record SiteResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    GeoJsonPolygonResponse Boundary,
    DateTimeOffset CreatedAtUtc)
{
    public static SiteResponse FromDomain(Site site)
    {
        return new SiteResponse(
            site.Id,
            site.ProjectId,
            site.Name,
            GeoJsonPolygonMapper.ToResponse(site.Boundary),
            site.CreatedAtUtc);
    }
}