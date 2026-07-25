using FormaGeo.Application.Analyses;
using System.Net.Http.Json;
using System.Text.Json;

namespace FormaGeo.Infrastructure.Analyses;

public sealed class GeoprocessingAnalysisExecutor
    : IAnalysisExecutor, IDisposable
{
    private readonly HttpClient _httpClient;

    public GeoprocessingAnalysisExecutor(
        string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<AnalysisExecutionResult> ExecuteAsync(
        AnalysisExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "analyses/run",
                new
                {
                    analysisId = request.AnalysisId,
                    siteId = request.SiteId,
                    analysisType =
                        request.AnalysisType.ToString(),
                    analysisVersion =
                        request.AnalysisVersion,
                    siteGeometry = request.SiteGeometry,
                    inputParameters =
                        request.InputParameters
                },
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AnalysisExecutionException(
                "The geoprocessing service could not be reached. " +
                "Confirm that it is running and try again.",
                exception);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnalysisExecutionException(
                "The geoprocessing service did not respond before the timeout.",
                exception);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AnalysisExecutionException(
                    ExtractServiceError(
                        content,
                        $"Geoprocessing failed with HTTP " +
                        $"{(int)response.StatusCode}."));
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                if (!root.TryGetProperty(
                        "result",
                        out var result))
                {
                    throw new AnalysisExecutionException(
                        "The geoprocessing service returned no analysis result.");
                }

                return new AnalysisExecutionResult(
                    result.Clone());
            }
            catch (JsonException exception)
            {
                throw new AnalysisExecutionException(
                    "The geoprocessing service returned invalid JSON.",
                    exception);
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string ExtractServiceError(
        string content,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.TryGetProperty(
                    "error",
                    out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? fallback;
                }

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty(
                        "message",
                        out var message))
                {
                    return message.GetString() ?? fallback;
                }
            }

            if (root.TryGetProperty(
                    "detail",
                    out var detail))
            {
                return detail.ValueKind == JsonValueKind.String
                    ? detail.GetString() ?? fallback
                    : detail.GetRawText();
            }
        }
        catch (JsonException)
        {
            // Use the HTTP fallback when the error body is not JSON.
        }

        return fallback;
    }
}
