using FormaGeo.Application.Sites;
using FormaGeo.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class SiteRepository : ISiteRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public SiteRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Site site,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Sites.Add(site);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public Task<Site?> GetByIdAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                site => site.Id == siteId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Site>>
        GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sites
            .AsNoTracking()
            .Where(site =>
                site.ProjectId == projectId)
            .OrderByDescending(site =>
                site.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var site = await _dbContext.Sites
            .SingleOrDefaultAsync(
                site => site.Id == siteId,
                cancellationToken);

        if (site is null)
        {
            return false;
        }

        _dbContext.Sites.Remove(site);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }
}