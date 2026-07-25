using FormaGeo.Application.Analyses;
using FormaGeo.Domain.Analyses;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class AnalysisRunRepository
    : IAnalysisRunRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public AnalysisRunRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        AnalysisRun analysisRun,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AnalysisRuns.Add(analysisRun);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<AnalysisRun?> GetByIdAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.AnalysisRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                analysisRun =>
                    analysisRun.Id == analysisId,
                cancellationToken);
    }

    public Task<AnalysisRun?> GetForUpdateAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.AnalysisRuns
            .SingleOrDefaultAsync(
                analysisRun =>
                    analysisRun.Id == analysisId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AnalysisRun>>
        GetBySiteIdAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.AnalysisRuns
            .AsNoTracking()
            .Where(analysisRun =>
                analysisRun.SiteId == siteId)
            .OrderByDescending(analysisRun =>
                analysisRun.RequestedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<AnalysisRun?> GetNextPendingForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.AnalysisRuns
            .Where(analysisRun =>
                analysisRun.Status == AnalysisStatus.Pending)
            .OrderBy(analysisRun =>
                analysisRun.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RecoverInterruptedAsync(
        CancellationToken cancellationToken = default)
    {
        var interruptedRuns = await _dbContext.AnalysisRuns
            .Where(analysisRun =>
                analysisRun.Status == AnalysisStatus.Running)
            .ToListAsync(cancellationToken);

        foreach (var analysisRun in interruptedRuns)
        {
            analysisRun.ReturnToPending();
        }

        if (interruptedRuns.Count > 0)
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
    }

    public Task ReloadAsync(
        AnalysisRun analysisRun,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Entry(analysisRun)
            .ReloadAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
