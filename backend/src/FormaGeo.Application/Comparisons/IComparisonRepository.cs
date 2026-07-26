using FormaGeo.Domain.Comparisons;

namespace FormaGeo.Application.Comparisons;

public interface IComparisonRepository
{
    Task AddAsync(
        SiteComparison comparison,
        CancellationToken cancellationToken = default);

    Task<SiteComparison?> GetByIdAsync(
        Guid comparisonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SiteComparison>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
