using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.RestoreSite;

public sealed class RestoreSiteHandler
{
    private readonly ISiteRepository _siteRepository;

    public RestoreSiteHandler(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<SiteResponse> HandleAsync(
        Guid siteId,
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

        site.Restore();
        await _siteRepository.SaveChangesAsync(cancellationToken);

        return SiteResponse.FromDomain(site);
    }
}
