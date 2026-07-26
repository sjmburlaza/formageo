using FormaGeo.Domain.Reports;

namespace FormaGeo.Application.Reports;

public interface IReportRepository
{
    Task AddAsync(
        GeneratedReport report,
        CancellationToken cancellationToken = default);

    Task<GeneratedReport?> GetByIdAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedReport>> GetForSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedReport>> GetForComparisonAsync(
        Guid comparisonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedReport>> GetForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
