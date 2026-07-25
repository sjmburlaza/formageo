using FormaGeo.Domain.Analyses;
using System.Text.Json;

namespace FormaGeo.Application.Analyses;

public sealed record CreateAnalysisRequest(
    AnalysisType AnalysisType,
    JsonElement InputParameters);

public sealed record AnalysisRunResponse(
    Guid Id,
    Guid SiteId,
    AnalysisType AnalysisType,
    AnalysisStatus Status,
    JsonElement InputParameters,
    JsonElement? Result,
    string? ErrorMessage,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string AnalysisVersion)
{
    public static AnalysisRunResponse FromDomain(
        AnalysisRun analysisRun)
    {
        return new AnalysisRunResponse(
            analysisRun.Id,
            analysisRun.SiteId,
            analysisRun.AnalysisType,
            analysisRun.Status,
            ParseJson(analysisRun.InputParametersJson),
            analysisRun.ResultJson is null
                ? null
                : ParseJson(analysisRun.ResultJson),
            analysisRun.ErrorMessage,
            analysisRun.RequestedAtUtc,
            analysisRun.StartedAtUtc,
            analysisRun.CompletedAtUtc,
            analysisRun.AnalysisVersion);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

public sealed record AnalysisDefinitionResponse(
    AnalysisType AnalysisType,
    string Name,
    string Description,
    IReadOnlyList<string> RequiredInputs,
    string EstimatedComplexity,
    string AnalysisVersion,
    IReadOnlyList<AnalysisParameterDefinitionResponse> Parameters);

public sealed record AnalysisParameterDefinitionResponse(
    string Name,
    string Label,
    string Description,
    string Type,
    bool Required,
    object? DefaultValue,
    double? Minimum = null,
    double? Maximum = null);

public sealed record AnalysisExecutionRequest(
    Guid AnalysisId,
    Guid SiteId,
    AnalysisType AnalysisType,
    string AnalysisVersion,
    JsonElement SiteGeometry,
    JsonElement InputParameters);

public sealed record AnalysisExecutionResult(
    JsonElement Result);

public interface IAnalysisExecutor
{
    Task<AnalysisExecutionResult> ExecuteAsync(
        AnalysisExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AnalysisExecutionException : Exception
{
    public AnalysisExecutionException(string message)
        : base(message)
    {
    }

    public AnalysisExecutionException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class AnalysisValidationException : Exception
{
    public AnalysisValidationException(
        string field,
        string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
