using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Application.Sites.Mapping;

namespace FormaGeo.Application.Sites.UpdateSiteBoundary;

public sealed class UpdateSiteBoundaryHandler
{
    private readonly ISiteRepository _siteRepository;

    public UpdateSiteBoundaryHandler(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<SiteResponse> HandleAsync(
        Guid siteId,
        UpdateSiteBoundaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetForUpdateAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            throw new KeyNotFoundException(
                $"Site '{siteId}' was not found.");
        }

        var boundary = GeoJsonPolygonMapper.ToDomain(request.Boundary);
        site.UpdateBoundary(boundary);
        await _siteRepository.SaveChangesAsync(cancellationToken);

        return SiteResponse.FromDomain(site);
    }
}
