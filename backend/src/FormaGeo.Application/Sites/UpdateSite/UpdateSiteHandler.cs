using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.UpdateSite;

public sealed class UpdateSiteHandler
{
    private readonly ISiteRepository _siteRepository;

    public UpdateSiteHandler(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<SiteResponse> HandleAsync(
        Guid siteId,
        UpdateSiteRequest request,
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

        site.Rename(request.Name ?? string.Empty);
        await _siteRepository.SaveChangesAsync(cancellationToken);

        return SiteResponse.FromDomain(site);
    }
}
