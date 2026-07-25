using FormaGeo.Domain.Sites;

namespace FormaGeo.Application.Sites;

public interface ISiteRepository
{
    Task AddAsync(
        Site site,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<Site> sites,
        CancellationToken cancellationToken = default);

    Task<Site?> GetByIdAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<Site?> GetForUpdateAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Site>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>>
        GetCountsByProjectIdAsync(
            IEnumerable<Guid> projectIds,
            CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
