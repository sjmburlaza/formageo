using FormaGeo.Domain.Scoring;

namespace FormaGeo.Application.Scoring;

public interface IScoringRepository
{
    Task AddScenarioAsync(
        ScoringScenario scenario,
        ScoringModel model,
        CancellationToken cancellationToken = default);

    Task<ScoringScenario?> GetScenarioAsync(
        Guid scenarioId,
        bool forUpdate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoringScenario>> GetScenariosByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ScoringModel?> GetLatestModelAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default);

    Task<ScoringModel?> GetModelVersionAsync(
        Guid scenarioId,
        int version,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ScoringModel>>
        GetLatestModelsAsync(
            IEnumerable<Guid> scenarioIds,
            CancellationToken cancellationToken = default);

    Task AddModelAsync(
        ScoringModel model,
        CancellationToken cancellationToken = default);

    Task AddResultsAsync(
        IEnumerable<ScoringResult> results,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoringResult>> GetResultsAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default);
}
