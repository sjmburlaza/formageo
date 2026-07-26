using System.Text.Json;

namespace FormaGeo.Domain.Reports;

public sealed class GeneratedReport
{
    private GeneratedReport()
    {
        // Required by Entity Framework Core.
    }

    private GeneratedReport(
        Guid id,
        Guid projectId,
        ReportSourceType sourceType,
        Guid? siteId,
        Guid? comparisonId,
        ReportFormat format,
        string title,
        string fileName,
        string contentType,
        string storageKey,
        long fileSizeBytes,
        string sectionsJson,
        string brandingJson,
        DateTimeOffset generatedAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        SourceType = sourceType;
        SiteId = siteId;
        ComparisonId = comparisonId;
        Format = format;
        Title = title;
        FileName = fileName;
        ContentType = contentType;
        StorageKey = storageKey;
        FileSizeBytes = fileSizeBytes;
        SectionsJson = sectionsJson;
        BrandingJson = brandingJson;
        GeneratedAtUtc = generatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public ReportSourceType SourceType { get; private set; }

    public Guid? SiteId { get; private set; }

    public Guid? ComparisonId { get; private set; }

    public ReportFormat Format { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } =
        "application/octet-stream";

    public string StorageKey { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public string SectionsJson { get; private set; } = "[]";

    public string BrandingJson { get; private set; } = "{}";

    public DateTimeOffset GeneratedAtUtc { get; private set; }

    public static GeneratedReport Create(
        Guid id,
        Guid projectId,
        ReportSourceType sourceType,
        Guid? siteId,
        Guid? comparisonId,
        ReportFormat format,
        string title,
        string fileName,
        string contentType,
        string storageKey,
        long fileSizeBytes,
        string sectionsJson,
        string brandingJson,
        DateTimeOffset generatedAtUtc)
    {
        if (id == Guid.Empty || projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "Report and project IDs are required.");
        }

        if (sourceType == ReportSourceType.Site &&
            !siteId.HasValue)
        {
            throw new ArgumentException(
                "A site report requires a site ID.",
                nameof(siteId));
        }

        if (sourceType == ReportSourceType.Comparison &&
            !comparisonId.HasValue)
        {
            throw new ArgumentException(
                "A comparison report requires a comparison ID.",
                nameof(comparisonId));
        }

        if (fileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                "A generated report cannot be empty.");
        }

        ValidateText(title, nameof(title), 200);
        ValidateText(fileName, nameof(fileName), 240);
        ValidateText(contentType, nameof(contentType), 120);
        ValidateText(storageKey, nameof(storageKey), 500);
        ValidateJson(sectionsJson, nameof(sectionsJson));
        ValidateJson(brandingJson, nameof(brandingJson));

        return new GeneratedReport(
            id,
            projectId,
            sourceType,
            siteId,
            comparisonId,
            format,
            title.Trim(),
            fileName.Trim(),
            contentType.Trim(),
            storageKey.Trim(),
            fileSizeBytes,
            sectionsJson,
            brandingJson,
            generatedAtUtc);
    }

    private static void ValidateText(
        string value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A value is required.",
                parameterName);
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }

    private static void ValidateJson(
        string value,
        string parameterName)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
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
