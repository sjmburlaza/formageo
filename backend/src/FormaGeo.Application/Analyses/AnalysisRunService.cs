using FormaGeo.Application.Sites;
using FormaGeo.Domain.Analyses;
using System.Text.Json;

namespace FormaGeo.Application.Analyses;

public sealed class AnalysisRunService
{
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly ISiteRepository _siteRepository;

    public AnalysisRunService(
        IAnalysisRunRepository analysisRunRepository,
        ISiteRepository siteRepository)
    {
        _analysisRunRepository = analysisRunRepository;
        _siteRepository = siteRepository;
    }

    public async Task<AnalysisRunResponse> CreateAsync(
        Guid siteId,
        CreateAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByIdAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            throw new KeyNotFoundException(
                $"Site '{siteId}' was not found.");
        }

        if (!Enum.IsDefined(request.AnalysisType))
        {
            throw new AnalysisValidationException(
                "analysisType",
                "A supported analysis type is required.");
        }

        var definition = AnalysisCatalog.Get(
            request.AnalysisType);
        var parameters =
            AnalysisCatalog.ValidateAndNormalizeParameters(
                request.AnalysisType,
                request.InputParameters);
        var analysisRun = AnalysisRun.Create(
            siteId,
            request.AnalysisType,
            parameters.GetRawText(),
            definition.AnalysisVersion);

        await _analysisRunRepository.AddAsync(
            analysisRun,
            cancellationToken);

        return AnalysisRunResponse.FromDomain(analysisRun);
    }

    public async Task<AnalysisRunResponse?> GetAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var analysisRun =
            await _analysisRunRepository.GetByIdAsync(
                analysisId,
                cancellationToken);

        return analysisRun is null
            ? null
            : AnalysisRunResponse.FromDomain(analysisRun);
    }

    public async Task<IReadOnlyList<AnalysisRunResponse>>
        GetForSiteAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByIdAsync(
            siteId,
            cancellationToken);

        if (site is null)
        {
            throw new KeyNotFoundException(
                $"Site '{siteId}' was not found.");
        }

        var analysisRuns =
            await _analysisRunRepository.GetBySiteIdAsync(
                siteId,
                cancellationToken);

        return analysisRuns
            .Select(AnalysisRunResponse.FromDomain)
            .ToArray();
    }

    public async Task<bool> CancelAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var analysisRun =
            await _analysisRunRepository.GetForUpdateAsync(
                analysisId,
                cancellationToken);

        if (analysisRun is null)
        {
            throw new KeyNotFoundException(
                $"Analysis run '{analysisId}' was not found.");
        }

        var cancelled = analysisRun.Cancel();

        if (cancelled)
        {
            await _analysisRunRepository.SaveChangesAsync(
                cancellationToken);
        }

        return cancelled;
    }
}
