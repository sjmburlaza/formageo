using FormaGeo.Application.Reports;
using FormaGeo.Application.Scoring;
using FormaGeo.Domain.Analyses;
using FormaGeo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormaGeo.Infrastructure.Reports;

public sealed class ReportEvidenceReader
    : IReportEvidenceReader
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private readonly FormaGeoDbContext _dbContext;

    public ReportEvidenceReader(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReportEvidenceSnapshot> GetForSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var site = await _dbContext.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == siteId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Site '{siteId}' was not found.");
        var runs = await _dbContext.AnalysisRuns
            .AsNoTracking()
            .Where(run =>
                run.SiteId == siteId &&
                run.Status == AnalysisStatus.Completed &&
                run.ResultJson != null)
            .OrderByDescending(run => run.CompletedAtUtc)
            .ToListAsync(cancellationToken);
        var latestRuns = runs
            .GroupBy(run => run.AnalysisType)
            .Select(group => group.First())
            .ToArray();
        var findings = new List<ReportFindingResponse>();
        var sources = new List<ReportDataSourceResponse>();
        var limitations = new List<string>();
        var features = new List<AnalysisGeoJsonFeature>();

        foreach (var run in latestRuns)
        {
            using var document = JsonDocument.Parse(
                run.ResultJson!);
            var root = document.RootElement;

            ReadLimitations(root, limitations);
            ReadSourceMetadata(root, sources);
            AddGeometryFeature(
                run.Id,
                run.AnalysisType.ToString(),
                run.AnalysisVersion,
                root,
                "resultGeometry",
                new Dictionary<string, object?>
                {
                    ["siteId"] = site.Id,
                    ["siteName"] = site.Name,
                    ["analysisType"] = run.AnalysisType.ToString()
                },
                features);

            if (!root.TryGetProperty(
                    "results",
                    out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var result in results.EnumerateArray())
            {
                var name = StringProperty(result, "name")
                    ?? AnalysisName(run.AnalysisType);
                var category = StringProperty(result, "category")
                    ?? Category(run.AnalysisType);
                var severity = StringProperty(result, "severity")
                    ?? "Informational";
                var summary = StringProperty(result, "summary")
                    ?? "Analysis completed.";
                var classification =
                    StringProperty(result, "classification")
                    ?? "Not classified";
                var finding = new ReportFindingResponse(
                    site.Id,
                    site.Name,
                    category,
                    name,
                    severity,
                    summary,
                    classification,
                    NumberProperty(result, "sitePercent"),
                    run.AnalysisVersion);
                findings.Add(finding);
                ReadLimitations(result, limitations);
                ReadEvidenceSource(result, sources);
                AddGeometryFeature(
                    run.Id,
                    run.AnalysisType.ToString(),
                    run.AnalysisVersion,
                    result,
                    "resultGeometry",
                    new Dictionary<string, object?>
                    {
                        ["siteId"] = site.Id,
                        ["siteName"] = site.Name,
                        ["analysisType"] =
                            run.AnalysisType.ToString(),
                        ["findingName"] = name,
                        ["category"] = category,
                        ["severity"] = severity,
                        ["classification"] = classification,
                        ["sitePercent"] =
                            NumberProperty(result, "sitePercent")
                    },
                    features);
            }
        }

        var score = await ReadLatestScoreAsync(
            siteId,
            cancellationToken);
        if (score is not null)
        {
            sources.AddRange(
                score.Metrics
                    .Select(metric =>
                        new ReportDataSourceResponse(
                            metric.DataSource,
                            metric.DataSource,
                            $"Suitability metric source for {metric.Name}.",
                            metric.DataVersion,
                            null,
                            null,
                            null)));
        }

        return new ReportEvidenceSnapshot(
            score,
            findings,
            sources
                .DistinctBy(source => new
                {
                    source.Dataset,
                    source.Version,
                    source.SourceUrl
                })
                .OrderBy(source => source.Dataset)
                .ToArray(),
            limitations
                .Where(limitation =>
                    !string.IsNullOrWhiteSpace(limitation))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            features);
    }

    private async Task<ReportScoreResponse?> ReadLatestScoreAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var result = await _dbContext.ScoringResults
            .AsNoTracking()
            .Where(candidate => candidate.SiteId == siteId)
            .OrderByDescending(candidate =>
                candidate.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        var model = await _dbContext.ScoringModels
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == result.ScoringModelId,
                cancellationToken);
        var snapshot =
            JsonSerializer.Deserialize<ScoringBreakdownSnapshot>(
                result.BreakdownJson,
                SnapshotJsonOptions);
        if (model is null || snapshot is null)
        {
            return null;
        }

        return new ReportScoreResponse(
            result.SiteId,
            snapshot.SiteName,
            result.OverallScore,
            result.Rating,
            result.IsScoreable,
            model.Version,
            model.Name,
            result.CalculatedAtUtc,
            snapshot.Criteria
                .Select(metric =>
                    new ReportScoreMetricResponse(
                        metric.Name,
                        metric.RawValue,
                        metric.Unit,
                        metric.NormalizedScore,
                        metric.Contribution,
                        metric.DataSource,
                        metric.DataVersion,
                        metric.Explanation))
                .ToArray(),
            snapshot.Strengths,
            snapshot.Weaknesses,
            snapshot.MissingInformation);
    }

    private static void ReadSourceMetadata(
        JsonElement root,
        ICollection<ReportDataSourceResponse> sources)
    {
        if (!root.TryGetProperty(
                "sourceMetadata",
                out var metadata) ||
            metadata.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var source in metadata.EnumerateArray())
        {
            sources.Add(ToDataSource(source));
        }
    }

    private static void ReadEvidenceSource(
        JsonElement result,
        ICollection<ReportDataSourceResponse> sources)
    {
        if (result.TryGetProperty(
                "source",
                out var source) &&
            source.ValueKind == JsonValueKind.Object)
        {
            sources.Add(ToDataSource(source));
        }
    }

    private static ReportDataSourceResponse ToDataSource(
        JsonElement source)
    {
        var dataset =
            StringProperty(source, "dataset")
            ?? StringProperty(source, "name")
            ?? "Unspecified dataset";
        var organization =
            StringProperty(source, "organization")
            ?? "Unspecified publisher";

        return new ReportDataSourceResponse(
            dataset,
            organization,
            $"{dataset}, {organization}.",
            StringProperty(source, "dataVersion"),
            StringProperty(source, "publishedDate"),
            StringProperty(source, "license"),
            StringProperty(source, "sourceUrl"));
    }

    private static void ReadLimitations(
        JsonElement element,
        ICollection<string> limitations)
    {
        if (!element.TryGetProperty(
                "limitations",
                out var value))
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.String &&
            value.GetString() is { } single)
        {
            limitations.Add(single);
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                item.GetString() is { } text)
            {
                limitations.Add(text);
            }
        }
    }

    private static void AddGeometryFeature(
        Guid analysisId,
        string analysisType,
        string analysisVersion,
        JsonElement element,
        string propertyName,
        IReadOnlyDictionary<string, object?> properties,
        ICollection<AnalysisGeoJsonFeature> features)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var geometry) ||
            geometry.ValueKind is JsonValueKind.Null
                or JsonValueKind.Undefined)
        {
            return;
        }

        features.Add(
            new AnalysisGeoJsonFeature(
                analysisId,
                analysisType,
                analysisVersion,
                geometry.Clone(),
                properties));
    }

    private static string AnalysisName(AnalysisType analysisType)
    {
        return analysisType switch
        {
            AnalysisType.HazardExposure =>
                "Hazard exposure",
            AnalysisType.Zoning =>
                "Planning and zoning",
            AnalysisType.Terrain =>
                "Terrain profile",
            AnalysisType.Accessibility =>
                "Accessibility",
            AnalysisType.NearbyFacilities =>
                "Nearby facilities",
            AnalysisType.Suitability =>
                "Suitability",
            _ => "Site geometry"
        };
    }

    private static string Category(AnalysisType analysisType)
    {
        return analysisType switch
        {
            AnalysisType.HazardExposure => "Hazards",
            AnalysisType.Zoning => "Planning",
            AnalysisType.Terrain => "Terrain",
            AnalysisType.Accessibility => "Accessibility",
            AnalysisType.NearbyFacilities => "Facilities",
            AnalysisType.Suitability => "Suitability",
            _ => "Geometry"
        };
    }

    private static string? StringProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static double? NumberProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var value)
                ? value
                : null;
    }
}
