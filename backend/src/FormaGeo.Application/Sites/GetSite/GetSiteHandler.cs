using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.GetSite;

public sealed class GetSiteHandler
{
    private readonly ISiteRepository _siteRepository;

    public GetSiteHandler(
        ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<SiteResponse?> HandleAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByIdAsync(
            siteId,
            cancellationToken);

        return site is null
            ? null
            : SiteResponse.FromDomain(site);
    }
}