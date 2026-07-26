using FormaGeo.Application.Scoring;
using FormaGeo.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Repositories;

public sealed class ScoringRepository : IScoringRepository
{
    private readonly FormaGeoDbContext _dbContext;

    public ScoringRepository(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddScenarioAsync(
        ScoringScenario scenario,
        ScoringModel model,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ScoringScenarios.Add(scenario);
        _dbContext.ScoringModels.Add(model);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ScoringScenario?> GetScenarioAsync(
        Guid scenarioId,
        bool forUpdate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ScoringScenarios
            .Where(scenario => scenario.Id == scenarioId);

        return (forUpdate
                ? query
                : query.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoringScenario>>
        GetScenariosByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScoringScenarios
            .AsNoTracking()
            .Where(scenario =>
                scenario.ProjectId == projectId)
            .OrderByDescending(scenario =>
                scenario.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<ScoringModel?> GetLatestModelAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ScoringModels
            .AsNoTracking()
            .Include(model => model.Criteria)
            .Where(model =>
                model.ScoringScenarioId == scenarioId)
            .OrderByDescending(model => model.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ScoringModel?> GetModelVersionAsync(
        Guid scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ScoringModels
            .AsNoTracking()
            .Include(model => model.Criteria)
            .SingleOrDefaultAsync(
                model =>
                    model.ScoringScenarioId == scenarioId &&
                    model.Version == version,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ScoringModel>>
        GetLatestModelsAsync(
            IEnumerable<Guid> scenarioIds,
            CancellationToken cancellationToken = default)
    {
        var ids = scenarioIds.Distinct().ToArray();
        var models = await _dbContext.ScoringModels
            .AsNoTracking()
            .Include(model => model.Criteria)
            .Where(model =>
                ids.Contains(model.ScoringScenarioId))
            .OrderByDescending(model => model.Version)
            .ToListAsync(cancellationToken);

        return models
            .GroupBy(model => model.ScoringScenarioId)
            .ToDictionary(
                group => group.Key,
                group => group.First());
    }

    public async Task AddModelAsync(
        ScoringModel model,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ScoringModels.Add(model);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddResultsAsync(
        IEnumerable<ScoringResult> results,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ScoringResults.AddRange(results);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoringResult>>
        GetResultsAsync(
            Guid scenarioId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScoringResults
            .AsNoTracking()
            .Where(result =>
                result.ScoringScenarioId == scenarioId)
            .OrderByDescending(result =>
                result.CalculatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);
    }
}
