using FormaGeo.Domain.Scoring;
using FormaGeo.Domain.Sites;

namespace FormaGeo.Application.Scoring;

public interface IScoringValueProvider
{
    Task<IReadOnlyList<CriterionObservation>> GetValuesAsync(
        Site site,
        IReadOnlyCollection<ScoringCriterion> criteria,
        CancellationToken cancellationToken = default);
}
