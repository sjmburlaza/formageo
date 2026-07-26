using FormaGeo.Application.Reports;
using FormaGeo.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public ReportRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        GeneratedReport report,
        CancellationToken cancellationToken = default)
    {
        _dbContext.GeneratedReports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<GeneratedReport?> GetByIdAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.GeneratedReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report => report.Id == reportId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedReport>> GetForSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return await RecentReports()
            .Where(report => report.SiteId == siteId)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedReport>>
        GetForComparisonAsync(
            Guid comparisonId,
            CancellationToken cancellationToken = default)
    {
        return await RecentReports()
            .Where(report =>
                report.ComparisonId == comparisonId)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedReport>> GetForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await RecentReports()
            .Where(report => report.ProjectId == projectId)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<GeneratedReport> RecentReports()
    {
        return _dbContext.GeneratedReports
            .AsNoTracking()
            .OrderByDescending(report => report.GeneratedAtUtc);
    }
}
