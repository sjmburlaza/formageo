using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.SiteImports;

public enum SiteImportMode
{
    Separate,
    Merge
}

public sealed record SiteImportRequest(
    string FileName,
    string Content,
    string? NamePattern,
    SiteImportMode Mode,
    IReadOnlyList<int>? SelectedFeatureIndexes);

public sealed record SiteImportSkippedFeature(
    int? FeatureIndex,
    string? FeatureName,
    string Reason);

public sealed record SiteImportResult(
    Guid ImportId,
    Guid ProjectId,
    int FeatureCount,
    int InvalidFeatureCount,
    string DetectedCoordinateSystem,
    IReadOnlyList<SiteResponse> ImportedSites,
    IReadOnlyList<SiteImportSkippedFeature> SkippedFeatures,
    IReadOnlyList<string> Warnings);

public sealed class SiteImportException : ArgumentException
{
    public SiteImportException(
        string field,
        string problem,
        string message)
        : base(message, field)
    {
        Field = field;
        Problem = problem;
    }

    public string Field { get; }

    public string Problem { get; }
}
