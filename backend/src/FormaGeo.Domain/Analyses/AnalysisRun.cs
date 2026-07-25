using System.Text.Json;

namespace FormaGeo.Domain.Analyses;

public sealed class AnalysisRun
{
    private AnalysisRun()
    {
        // Required by Entity Framework Core.
    }

    private AnalysisRun(
        Guid id,
        Guid siteId,
        AnalysisType analysisType,
        string inputParametersJson,
        string analysisVersion,
        DateTimeOffset requestedAtUtc)
    {
        Id = id;
        SiteId = siteId;
        AnalysisType = analysisType;
        Status = AnalysisStatus.Pending;
        InputParametersJson = inputParametersJson;
        AnalysisVersion = analysisVersion;
        RequestedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SiteId { get; private set; }

    public AnalysisType AnalysisType { get; private set; }

    public AnalysisStatus Status { get; private set; }

    public string InputParametersJson { get; private set; } = "{}";

    public string? ResultJson { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string AnalysisVersion { get; private set; } = string.Empty;

    public static AnalysisRun Create(
        Guid siteId,
        AnalysisType analysisType,
        string inputParametersJson,
        string analysisVersion)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException(
                "A site ID is required.",
                nameof(siteId));
        }

        ValidateJson(inputParametersJson, nameof(inputParametersJson));

        if (string.IsNullOrWhiteSpace(analysisVersion))
        {
            throw new ArgumentException(
                "An analysis version is required.",
                nameof(analysisVersion));
        }

        return new AnalysisRun(
            Guid.NewGuid(),
            siteId,
            analysisType,
            inputParametersJson,
            analysisVersion.Trim(),
            DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        if (Status != AnalysisStatus.Pending)
        {
            throw InvalidTransition(AnalysisStatus.Running);
        }

        Status = AnalysisStatus.Running;
        StartedAtUtc = DateTimeOffset.UtcNow;
        CompletedAtUtc = null;
        ErrorMessage = null;
    }

    public void Complete(string resultJson)
    {
        if (Status != AnalysisStatus.Running)
        {
            throw InvalidTransition(AnalysisStatus.Completed);
        }

        ValidateJson(resultJson, nameof(resultJson));
        ResultJson = resultJson;
        ErrorMessage = null;
        Status = AnalysisStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        if (Status is not (
                AnalysisStatus.Pending or AnalysisStatus.Running))
        {
            throw InvalidTransition(AnalysisStatus.Failed);
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException(
                "A failure message is required.",
                nameof(errorMessage));
        }

        Status = AnalysisStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        ResultJson = null;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool Cancel()
    {
        if (Status is AnalysisStatus.Completed
            or AnalysisStatus.Failed
            or AnalysisStatus.Cancelled)
        {
            return false;
        }

        Status = AnalysisStatus.Cancelled;
        ErrorMessage = null;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    public void ReturnToPending()
    {
        if (Status != AnalysisStatus.Running)
        {
            return;
        }

        Status = AnalysisStatus.Pending;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        ErrorMessage = null;
    }

    private InvalidOperationException InvalidTransition(
        AnalysisStatus target)
    {
        return new InvalidOperationException(
            $"Analysis run '{Id}' cannot transition from " +
            $"{Status} to {target}.");
    }

    private static void ValidateJson(
        string json,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                "JSON content is required.",
                parameterName);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "The value must contain valid JSON.",
                parameterName,
                exception);
        }
    }
}
