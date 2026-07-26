using FormaGeo.Application.Comparisons;
using FormaGeo.Domain.Comparisons;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class ComparisonRepository : IComparisonRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public ComparisonRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        SiteComparison comparison,
        CancellationToken cancellationToken = default)
    {
        _dbContext.SiteComparisons.Add(comparison);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SiteComparison?> GetByIdAsync(
        Guid comparisonId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SiteComparisons
            .AsNoTracking()
            .SingleOrDefaultAsync(
                comparison => comparison.Id == comparisonId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SiteComparison>>
        GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.SiteComparisons
            .AsNoTracking()
            .Where(comparison =>
                comparison.ProjectId == projectId)
            .OrderByDescending(comparison =>
                comparison.ComparisonDateUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }
}
