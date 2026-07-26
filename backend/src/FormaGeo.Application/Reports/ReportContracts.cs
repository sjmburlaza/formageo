using FormaGeo.Domain.Reports;
using System.Text.Json;

namespace FormaGeo.Application.Reports;

public enum ReportSectionKey
{
    Cover = 1,
    ExecutiveSummary = 2,
    SiteLocation = 3,
    GeometrySummary = 4,
    HazardFindings = 5,
    PlanningFindings = 6,
    SuitabilityScore = 7,
    SiteComparison = 8,
    DataSources = 9,
    Disclaimer = 10
}

public sealed record ReportSectionSelection(
    ReportSectionKey Key,
    bool Included,
    int SortOrder);

public sealed record ReportBrandingRequest(
    string OrganizationName,
    string PreparedBy,
    string AccentColor,
    string? FooterText);

public sealed record CreateReportRequest(
    string Title,
    ReportFormat Format,
    IReadOnlyList<ReportSectionSelection>? Sections,
    ReportBrandingRequest? Branding);

public sealed record ReportResponse(
    Guid Id,
    Guid ProjectId,
    ReportSourceType SourceType,
    Guid SourceId,
    ReportFormat Format,
    string Title,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ReportSectionSelection> Sections,
    ReportBrandingRequest Branding,
    string DownloadUrl);

public sealed record ReportDownload(
    Stream Content,
    string ContentType,
    string FileName,
    long FileSizeBytes);

public sealed record ReportPreviewResponse(
    ReportSourceType SourceType,
    Guid SourceId,
    Guid ProjectId,
    string ProjectName,
    string SourceName,
    string Title,
    DateTimeOffset GenerationDateUtc,
    ReportBrandingRequest Branding,
    IReadOnlyList<ReportPreviewSectionResponse> Sections,
    IReadOnlyList<ReportMapSiteResponse> MapSites,
    IReadOnlyList<ReportMeasurementResponse> Measurements,
    IReadOnlyList<ReportFindingResponse> Findings,
    ReportScoreResponse? SuitabilityScore,
    ReportComparisonResponse? Comparison,
    IReadOnlyList<ReportDataSourceResponse> DataSources,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<AnalysisGeoJsonFeature> AnalysisFeatures);

public sealed record ReportPreviewSectionResponse(
    ReportSectionKey Key,
    string Heading,
    string Summary,
    bool Included,
    int SortOrder);

public sealed record ReportMapSiteResponse(
    Guid SiteId,
    string SiteName,
    IReadOnlyList<IReadOnlyList<double>> Boundary);

public sealed record ReportMeasurementResponse(
    Guid SiteId,
    string SiteName,
    double? AreaHectares,
    double? PerimeterMetres,
    double? CentroidLongitude,
    double? CentroidLatitude,
    int VertexCount,
    string CoordinateSystem);

public sealed record ReportFindingResponse(
    Guid SiteId,
    string SiteName,
    string Category,
    string Name,
    string Severity,
    string Summary,
    string Classification,
    double? SitePercent,
    string AnalysisVersion);

public sealed record ReportScoreResponse(
    Guid SiteId,
    string SiteName,
    decimal? OverallScore,
    string Rating,
    bool IsScoreable,
    int ModelVersion,
    string ModelName,
    DateTimeOffset CalculatedAtUtc,
    IReadOnlyList<ReportScoreMetricResponse> Metrics,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingInformation);

public sealed record ReportScoreMetricResponse(
    string Name,
    decimal? RawValue,
    string Unit,
    decimal? NormalizedScore,
    decimal Contribution,
    string DataSource,
    string? DataVersion,
    string Explanation);

public sealed record ReportComparisonResponse(
    Guid ComparisonId,
    string ScenarioName,
    int ModelVersion,
    DateTimeOffset ComparisonDateUtc,
    IReadOnlyList<ReportComparisonSiteResponse> Sites,
    string RankingExplanation);

public sealed record ReportComparisonSiteResponse(
    int Rank,
    Guid SiteId,
    string SiteName,
    decimal? OverallScore,
    string Rating,
    IReadOnlyList<ReportScoreMetricResponse> Metrics);

public sealed record ReportDataSourceResponse(
    string Dataset,
    string Organization,
    string Attribution,
    string? Version,
    string? PublishedDate,
    string? License,
    string? SourceUrl);

public sealed record GeneratedReportFile(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed record StoredReportFile(
    string StorageKey,
    long FileSizeBytes);

public sealed record ReportEvidenceSnapshot(
    ReportScoreResponse? Score,
    IReadOnlyList<ReportFindingResponse> Findings,
    IReadOnlyList<ReportDataSourceResponse> DataSources,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<AnalysisGeoJsonFeature> AnalysisFeatures);

public sealed record AnalysisGeoJsonFeature(
    Guid AnalysisId,
    string AnalysisType,
    string AnalysisVersion,
    JsonElement Geometry,
    IReadOnlyDictionary<string, object?> Properties);

public sealed class ReportValidationException : Exception
{
    public ReportValidationException(
        string field,
        string message)
        : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}
