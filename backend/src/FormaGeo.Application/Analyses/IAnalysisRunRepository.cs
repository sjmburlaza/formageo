using FormaGeo.Domain.Analyses;

namespace FormaGeo.Application.Analyses;

public interface IAnalysisRunRepository
{
    Task AddAsync(
        AnalysisRun analysisRun,
        CancellationToken cancellationToken = default);

    Task<AnalysisRun?> GetByIdAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);

    Task<AnalysisRun?> GetForUpdateAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnalysisRun>> GetBySiteIdAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<AnalysisRun?> GetNextPendingForUpdateAsync(
        CancellationToken cancellationToken = default);

    Task RecoverInterruptedAsync(
        CancellationToken cancellationToken = default);

    Task ReloadAsync(
        AnalysisRun analysisRun,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
