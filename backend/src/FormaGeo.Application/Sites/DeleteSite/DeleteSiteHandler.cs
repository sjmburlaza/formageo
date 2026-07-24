namespace FormaGeo.Application.Sites.DeleteSite;

public sealed class DeleteSiteHandler
{
    private readonly ISiteRepository _siteRepository;

    public DeleteSiteHandler(
        ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public Task<bool> HandleAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return _siteRepository.DeleteAsync(
            siteId,
            cancellationToken);
    }
}