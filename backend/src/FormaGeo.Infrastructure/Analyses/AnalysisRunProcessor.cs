using FormaGeo.Application.Analyses;
using FormaGeo.Application.Sites;
using FormaGeo.Application.Sites.Mapping;
using FormaGeo.Domain.Analyses;
using System.Text.Json;

namespace FormaGeo.Infrastructure.Analyses;

public sealed class AnalysisRunProcessor
{
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IAnalysisExecutor _analysisExecutor;

    public AnalysisRunProcessor(
        IAnalysisRunRepository analysisRunRepository,
        ISiteRepository siteRepository,
        IAnalysisExecutor analysisExecutor)
    {
        _analysisRunRepository = analysisRunRepository;
        _siteRepository = siteRepository;
        _analysisExecutor = analysisExecutor;
    }

    public async Task<bool> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        var analysisRun =
            await _analysisRunRepository
                .GetNextPendingForUpdateAsync(
                    cancellationToken);

        if (analysisRun is null)
        {
            return false;
        }

        analysisRun.Start();
        await _analysisRunRepository.SaveChangesAsync(
            cancellationToken);

        try
        {
            var site = await _siteRepository.GetByIdAsync(
                analysisRun.SiteId,
                cancellationToken);

            if (site is null)
            {
                throw new AnalysisExecutionException(
                    "The site was removed before the analysis could run.");
            }

            var siteGeometry =
                JsonSerializer.SerializeToElement(
                    GeoJsonPolygonMapper.ToResponse(
                        site.Boundary),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    });
            using var parametersDocument =
                JsonDocument.Parse(
                    analysisRun.InputParametersJson);
            var executionResult =
                await _analysisExecutor.ExecuteAsync(
                    new AnalysisExecutionRequest(
                        analysisRun.Id,
                        analysisRun.SiteId,
                        analysisRun.AnalysisType,
                        analysisRun.AnalysisVersion,
                        siteGeometry,
                        parametersDocument.RootElement.Clone()),
                    cancellationToken);

            await _analysisRunRepository.ReloadAsync(
                analysisRun,
                cancellationToken);

            if (analysisRun.Status == AnalysisStatus.Running)
            {
                analysisRun.Complete(
                    executionResult.Result.GetRawText());
                await _analysisRunRepository.SaveChangesAsync(
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _analysisRunRepository.ReloadAsync(
                analysisRun,
                cancellationToken);

            if (analysisRun.Status == AnalysisStatus.Running)
            {
                analysisRun.Fail(
                    DiagnosticMessage(exception));
                await _analysisRunRepository.SaveChangesAsync(
                    cancellationToken);
            }
        }

        return true;
    }

    private static string DiagnosticMessage(Exception exception)
    {
        const int MaximumLength = 2000;
        var message = exception is AnalysisExecutionException
            ? exception.Message
            : "The analysis failed unexpectedly: " +
              exception.Message;

        return message.Length <= MaximumLength
            ? message
            : message[..MaximumLength];
    }
}
