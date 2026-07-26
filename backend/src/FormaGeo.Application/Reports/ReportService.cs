using FormaGeo.Application.Comparisons;
using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites;
using FormaGeo.Application.Sites.Summaries;
using FormaGeo.Domain.Reports;
using FormaGeo.Domain.Sites;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FormaGeo.Application.Reports;

public sealed partial class ReportService
{
    private const string DefaultDisclaimer =
        "This feasibility report is a screening-level decision aid. " +
        "It does not replace field verification, engineering design, " +
        "title review, permitting advice, or an assessment by a qualified professional.";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private static readonly ReportBrandingRequest DefaultBranding =
        new(
            "FormaGeo",
            "FormaGeo analysis team",
            "#0F766E",
            "Prepared with FormaGeo spatial decision support");

    private static readonly ReportSectionKey[] SectionKeys =
        Enum.GetValues<ReportSectionKey>();

    private static readonly ReportDataSourceResponse[] BaseDataSources =
    [
        new(
            "Saved site boundary",
            "FormaGeo project data",
            "Boundary coordinates supplied by the project user and stored in WGS 84 (EPSG:4326).",
            null,
            null,
            null,
            null),
        new(
            "Geodesic site measurements",
            "FormaGeo / PostGIS",
            "Area, perimeter, and centroid values calculated from the saved boundary using PostGIS geography operations.",
            null,
            null,
            null,
            null)
    ];

    private readonly IReportRepository _reportRepository;
    private readonly IReportFileStore _fileStore;
    private readonly IReportFileGenerator _fileGenerator;
    private readonly IReportEvidenceReader _evidenceReader;
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly ISiteSummaryReader _siteSummaryReader;
    private readonly ComparisonService _comparisonService;

    public ReportService(
        IReportRepository reportRepository,
        IReportFileStore fileStore,
        IReportFileGenerator fileGenerator,
        IReportEvidenceReader evidenceReader,
        IProjectRepository projectRepository,
        ISiteRepository siteRepository,
        ISiteSummaryReader siteSummaryReader,
        ComparisonService comparisonService)
    {
        _reportRepository = reportRepository;
        _fileStore = fileStore;
        _fileGenerator = fileGenerator;
        _evidenceReader = evidenceReader;
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
        _siteSummaryReader = siteSummaryReader;
        _comparisonService = comparisonService;
    }

    public Task<ReportPreviewResponse> PreviewSiteAsync(
        Guid siteId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateFormat(
            ReportSourceType.Site,
            request.Format);

        return BuildSiteSnapshotAsync(
            siteId,
            request,
            cancellationToken);
    }

    public async Task<ReportResponse> CreateForSiteAsync(
        Guid siteId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await PreviewSiteAsync(
            siteId,
            request,
            cancellationToken);

        return await PersistAsync(
            snapshot,
            request.Format,
            siteId,
            null,
            cancellationToken);
    }

    public Task<ReportPreviewResponse> PreviewComparisonAsync(
        Guid comparisonId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateFormat(
            ReportSourceType.Comparison,
            request.Format);

        return BuildComparisonSnapshotAsync(
            comparisonId,
            request,
            cancellationToken);
    }

    public async Task<ReportResponse> CreateForComparisonAsync(
        Guid comparisonId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await PreviewComparisonAsync(
            comparisonId,
            request,
            cancellationToken);

        return await PersistAsync(
            snapshot,
            request.Format,
            null,
            comparisonId,
            cancellationToken);
    }

    public async Task<ReportResponse> CreateProjectArchiveAsync(
        Guid projectId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateFormat(
            ReportSourceType.Project,
            request.Format);
        var snapshot = await BuildProjectSnapshotAsync(
            projectId,
            request,
            cancellationToken);

        return await PersistAsync(
            snapshot,
            request.Format,
            null,
            null,
            cancellationToken);
    }

    public async Task<ReportResponse?> GetAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(
            reportId,
            cancellationToken);

        return report is null
            ? null
            : ToResponse(report);
    }

    public async Task<ReportDownload?> DownloadAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(
            reportId,
            cancellationToken);
        if (report is null)
        {
            return null;
        }

        var stream = await _fileStore.OpenReadAsync(
            report.StorageKey,
            cancellationToken);

        return stream is null
            ? null
            : new ReportDownload(
                stream,
                report.ContentType,
                report.FileName,
                report.FileSizeBytes);
    }

    public Task<IReadOnlyList<ReportResponse>> GetForSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return MapHistoryAsync(
            _reportRepository.GetForSiteAsync(
                siteId,
                cancellationToken));
    }

    public Task<IReadOnlyList<ReportResponse>> GetForComparisonAsync(
        Guid comparisonId,
        CancellationToken cancellationToken = default)
    {
        return MapHistoryAsync(
            _reportRepository.GetForComparisonAsync(
                comparisonId,
                cancellationToken));
    }

    public Task<IReadOnlyList<ReportResponse>> GetForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return MapHistoryAsync(
            _reportRepository.GetForProjectAsync(
                projectId,
                cancellationToken));
    }

    private async Task<ReportPreviewResponse> BuildSiteSnapshotAsync(
        Guid siteId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var site = await _siteRepository.GetByIdAsync(
            siteId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Site '{siteId}' was not found.");
        var project = await _projectRepository.GetByIdAsync(
            site.ProjectId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Project '{site.ProjectId}' was not found.");
        var measurement = await GetMeasurementAsync(
            site,
            cancellationToken);
        var evidence = await _evidenceReader.GetForSiteAsync(
            siteId,
            cancellationToken);
        var branding = NormalizeBranding(request.Branding);
        var sections = NormalizeSections(
            request.Sections,
            ReportSourceType.Site,
            request.Format);
        var generationDate = DateTimeOffset.UtcNow;
        var title = NormalizeTitle(
            request.Title,
            $"{site.Name} feasibility report");
        var mapSites = new[]
        {
            ToMapSite(site)
        };
        var limitations = evidence.Limitations
            .Append(DefaultDisclaimer)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var dataSources = WithBaseDataSources(
            evidence.DataSources);

        return new ReportPreviewResponse(
            ReportSourceType.Site,
            site.Id,
            project.Id,
            project.Name,
            site.Name,
            title,
            generationDate,
            branding,
            BuildPreviewSections(
                sections,
                mapSites,
                new[] { measurement },
                evidence.Findings,
                evidence.Score,
                null,
                dataSources,
                limitations),
            mapSites,
            new[] { measurement },
            evidence.Findings,
            evidence.Score,
            null,
            dataSources,
            limitations,
            evidence.AnalysisFeatures);
    }

    private async Task<ReportPreviewResponse>
        BuildComparisonSnapshotAsync(
            Guid comparisonId,
            CreateReportRequest request,
            CancellationToken cancellationToken)
    {
        var comparison = await _comparisonService.GetAsync(
            comparisonId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Comparison '{comparisonId}' was not found.");
        var project = await _projectRepository.GetByIdAsync(
            comparison.ProjectId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Project '{comparison.ProjectId}' was not found.");
        var projectSites = await _siteRepository.GetByProjectIdAsync(
            project.Id,
            cancellationToken);
        var selectedIds = comparison.SelectedSiteIds.ToHashSet();
        var selectedOrder = comparison.SelectedSiteIds
            .Select((siteId, index) => new
            {
                siteId,
                index
            })
            .ToDictionary(item => item.siteId, item => item.index);
        var sites = projectSites
            .Where(site => selectedIds.Contains(site.Id))
            .OrderBy(site => selectedOrder[site.Id])
            .ToArray();
        var measurements = new List<ReportMeasurementResponse>();
        var findings = new List<ReportFindingResponse>();
        var sources = new List<ReportDataSourceResponse>();
        var limitations = new List<string>();
        var analysisFeatures =
            new List<AnalysisGeoJsonFeature>();

        foreach (var site in sites)
        {
            measurements.Add(
                await GetMeasurementAsync(
                    site,
                    cancellationToken));
            var evidence = await _evidenceReader.GetForSiteAsync(
                site.Id,
                cancellationToken);
            findings.AddRange(evidence.Findings);
            sources.AddRange(evidence.DataSources);
            limitations.AddRange(evidence.Limitations);
            analysisFeatures.AddRange(
                evidence.AnalysisFeatures);
        }

        var comparisonResponse = new ReportComparisonResponse(
            comparison.Id,
            comparison.ScenarioName,
            comparison.ModelVersion,
            comparison.ComparisonDateUtc,
            comparison.Rankings
                .Select(ranking =>
                    new ReportComparisonSiteResponse(
                        ranking.Rank,
                        ranking.SiteId,
                        ranking.SiteName,
                        ranking.OverallScore,
                        ranking.Rating,
                        ranking.Metrics
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
                            .ToArray()))
                .ToArray(),
            comparison.RankingExplanation);
        var dataSources = WithBaseDataSources(
            sources
            .Concat(
                comparison.DataVersions.Select(version =>
                    new ReportDataSourceResponse(
                        version.DataSource,
                        version.DataSource,
                        $"Metric source for {version.CriterionKey}.",
                        string.Join(", ", version.Versions),
                        null,
                        null,
                        null)))
            .DistinctBy(source => new
            {
                source.Dataset,
                source.Version
            })
            .OrderBy(source => source.Dataset));
        limitations.Add(DefaultDisclaimer);
        var normalizedLimitations = limitations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var branding = NormalizeBranding(request.Branding);
        var sections = NormalizeSections(
            request.Sections,
            ReportSourceType.Comparison,
            request.Format);
        var title = NormalizeTitle(
            request.Title,
            $"{project.Name} site comparison");
        var mapSites = sites
            .Select(ToMapSite)
            .ToArray();

        return new ReportPreviewResponse(
            ReportSourceType.Comparison,
            comparison.Id,
            project.Id,
            project.Name,
            $"{sites.Length} candidate sites",
            title,
            DateTimeOffset.UtcNow,
            branding,
            BuildPreviewSections(
                sections,
                mapSites,
                measurements,
                findings,
                null,
                comparisonResponse,
                dataSources,
                normalizedLimitations),
            mapSites,
            measurements,
            findings,
            null,
            comparisonResponse,
            dataSources,
            normalizedLimitations,
            analysisFeatures);
    }

    private async Task<ReportPreviewResponse> BuildProjectSnapshotAsync(
        Guid projectId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        var sites = await _siteRepository.GetByProjectIdAsync(
            projectId,
            cancellationToken);
        var measurements = new List<ReportMeasurementResponse>();
        var findings = new List<ReportFindingResponse>();
        var sources = new List<ReportDataSourceResponse>();
        var limitations = new List<string>();
        var analysisFeatures =
            new List<AnalysisGeoJsonFeature>();

        foreach (var site in sites)
        {
            measurements.Add(
                await GetMeasurementAsync(
                    site,
                    cancellationToken));
            var evidence = await _evidenceReader.GetForSiteAsync(
                site.Id,
                cancellationToken);
            findings.AddRange(evidence.Findings);
            sources.AddRange(evidence.DataSources);
            limitations.AddRange(evidence.Limitations);
            analysisFeatures.AddRange(
                evidence.AnalysisFeatures);
        }

        limitations.Add(DefaultDisclaimer);
        var normalizedLimitations = limitations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sections = NormalizeSections(
            request.Sections,
            ReportSourceType.Project,
            request.Format);
        var mapSites = sites.Select(ToMapSite).ToArray();
        var dataSources = WithBaseDataSources(
            sources
            .DistinctBy(source => new
            {
                source.Dataset,
                source.Version
            }));

        return new ReportPreviewResponse(
            ReportSourceType.Project,
            project.Id,
            project.Id,
            project.Name,
            project.Name,
            NormalizeTitle(
                request.Title,
                $"{project.Name} project data archive"),
            DateTimeOffset.UtcNow,
            NormalizeBranding(request.Branding),
            BuildPreviewSections(
                sections,
                mapSites,
                measurements,
                findings,
                null,
                null,
                dataSources,
                normalizedLimitations),
            mapSites,
            measurements,
            findings,
            null,
            null,
            dataSources,
            normalizedLimitations,
            analysisFeatures);
    }

    private async Task<ReportResponse> PersistAsync(
        ReportPreviewResponse snapshot,
        ReportFormat format,
        Guid? siteId,
        Guid? comparisonId,
        CancellationToken cancellationToken)
    {
        var generatedFile = _fileGenerator.Generate(
            format,
            snapshot);
        if (generatedFile.Content.Length == 0)
        {
            throw new InvalidOperationException(
                "The report generator returned an empty file.");
        }

        var reportId = Guid.NewGuid();
        var stored = await _fileStore.SaveAsync(
            reportId,
            generatedFile.FileName,
            generatedFile.Content,
            cancellationToken);
        var sectionSelections = snapshot.Sections
            .Select(section =>
                new ReportSectionSelection(
                    section.Key,
                    section.Included,
                    section.SortOrder))
            .ToArray();
        var report = GeneratedReport.Create(
            reportId,
            snapshot.ProjectId,
            snapshot.SourceType,
            siteId,
            comparisonId,
            format,
            snapshot.Title,
            generatedFile.FileName,
            generatedFile.ContentType,
            stored.StorageKey,
            stored.FileSizeBytes,
            JsonSerializer.Serialize(
                sectionSelections,
                JsonOptions),
            JsonSerializer.Serialize(
                snapshot.Branding,
                JsonOptions),
            snapshot.GenerationDateUtc);

        try
        {
            await _reportRepository.AddAsync(
                report,
                cancellationToken);
        }
        catch
        {
            await _fileStore.DeleteAsync(
                stored.StorageKey,
                CancellationToken.None);
            throw;
        }

        return ToResponse(report);
    }

    private async Task<ReportMeasurementResponse>
        GetMeasurementAsync(
            Site site,
            CancellationToken cancellationToken)
    {
        var summary = await _siteSummaryReader.GetAsync(
            site.Id,
            cancellationToken);

        return summary is null
            ? new ReportMeasurementResponse(
                site.Id,
                site.Name,
                null,
                null,
                null,
                null,
                site.Boundary.NumPoints,
                "WGS 84 (EPSG:4326)")
            : new ReportMeasurementResponse(
                site.Id,
                site.Name,
                summary.AreaSquareMetres / 10_000d,
                summary.PerimeterMetres,
                summary.CentroidLongitude,
                summary.CentroidLatitude,
                summary.VertexCount,
                summary.Srid == 4326
                    ? "WGS 84 (EPSG:4326)"
                    : $"EPSG:{summary.Srid}");
    }

    private static ReportMapSiteResponse ToMapSite(Site site)
    {
        return new ReportMapSiteResponse(
            site.Id,
            site.Name,
            site.Boundary.ExteriorRing.Coordinates
                .Select(coordinate =>
                    (IReadOnlyList<double>)new[]
                    {
                        coordinate.X,
                        coordinate.Y
                    })
                .ToArray());
    }

    private static IReadOnlyList<ReportPreviewSectionResponse>
        BuildPreviewSections(
            IReadOnlyList<ReportSectionSelection> sections,
            IReadOnlyList<ReportMapSiteResponse> mapSites,
            IReadOnlyList<ReportMeasurementResponse> measurements,
            IReadOnlyList<ReportFindingResponse> findings,
            ReportScoreResponse? score,
            ReportComparisonResponse? comparison,
            IReadOnlyList<ReportDataSourceResponse> sources,
            IReadOnlyList<string> limitations)
    {
        return sections
            .OrderBy(section => section.SortOrder)
            .Select(section =>
                new ReportPreviewSectionResponse(
                    section.Key,
                    Heading(section.Key),
                    Summary(
                        section.Key,
                        mapSites,
                        measurements,
                        findings,
                        score,
                        comparison,
                        sources,
                        limitations),
                    section.Included,
                    section.SortOrder))
            .ToArray();
    }

    private static string Heading(ReportSectionKey key)
    {
        return key switch
        {
            ReportSectionKey.Cover => "Cover",
            ReportSectionKey.ExecutiveSummary => "Executive summary",
            ReportSectionKey.SiteLocation => "Site location",
            ReportSectionKey.GeometrySummary => "Geometry summary",
            ReportSectionKey.HazardFindings => "Hazard findings",
            ReportSectionKey.PlanningFindings => "Planning findings",
            ReportSectionKey.SuitabilityScore => "Suitability score",
            ReportSectionKey.SiteComparison => "Site comparison",
            ReportSectionKey.DataSources => "Data sources",
            ReportSectionKey.Disclaimer => "Disclaimer",
            _ => key.ToString()
        };
    }

    private static string Summary(
        ReportSectionKey key,
        IReadOnlyList<ReportMapSiteResponse> mapSites,
        IReadOnlyList<ReportMeasurementResponse> measurements,
        IReadOnlyList<ReportFindingResponse> findings,
        ReportScoreResponse? score,
        ReportComparisonResponse? comparison,
        IReadOnlyList<ReportDataSourceResponse> sources,
        IReadOnlyList<string> limitations)
    {
        return key switch
        {
            ReportSectionKey.Cover =>
                "Title, project, source, branding, and generation date.",
            ReportSectionKey.ExecutiveSummary =>
                $"{mapSites.Count} site boundary record(s), " +
                $"{findings.Count} analysis finding(s), and " +
                $"{sources.Count} cited data source(s).",
            ReportSectionKey.SiteLocation =>
                $"{mapSites.Count} consistently framed boundary map(s) in WGS 84.",
            ReportSectionKey.GeometrySummary =>
                measurements.Count == 0
                    ? "No geometry measurements are available."
                    : "Geodesic area, perimeter, centroid, coordinate system, and vertex count.",
            ReportSectionKey.HazardFindings =>
                $"{findings.Count(finding => finding.Category.Equals("Hazards", StringComparison.OrdinalIgnoreCase))} hazard finding(s).",
            ReportSectionKey.PlanningFindings =>
                $"{findings.Count(finding => finding.Category.Equals("Planning", StringComparison.OrdinalIgnoreCase))} planning finding(s).",
            ReportSectionKey.SuitabilityScore =>
                score is null
                    ? "No saved site suitability result is available."
                    : score.IsScoreable
                        ? $"{score.OverallScore:0.##}/100 - {score.Rating}."
                        : "The latest saved suitability result is not scoreable.",
            ReportSectionKey.SiteComparison =>
                comparison is null
                    ? "No comparison snapshot is attached."
                    : $"{comparison.Sites.Count} sites ranked under model v{comparison.ModelVersion}.",
            ReportSectionKey.DataSources =>
                $"{sources.Count} attributed source(s), including versions and licenses when available.",
            ReportSectionKey.Disclaimer =>
                $"{limitations.Count} limitation and decision-use statement(s).",
            _ => string.Empty
        };
    }

    private static IReadOnlyList<ReportSectionSelection>
        NormalizeSections(
            IReadOnlyList<ReportSectionSelection>? requested,
            ReportSourceType sourceType,
            ReportFormat format)
    {
        var defaults = SectionKeys
            .Select((key, index) =>
                new ReportSectionSelection(
                    key,
                    IsIncludedByDefault(key, sourceType),
                    index))
            .ToArray();

        if (format != ReportFormat.Pdf || requested is null)
        {
            return defaults;
        }

        var duplicate = requested
            .GroupBy(section => section.Key)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ReportValidationException(
                "sections",
                $"Section '{duplicate.Key}' is listed more than once.");
        }

        if (requested.Any(section =>
                !Enum.IsDefined(section.Key)))
        {
            throw new ReportValidationException(
                "sections",
                "The report contains an unsupported section.");
        }

        var requestedByKey = requested.ToDictionary(
            section => section.Key);
        var normalized = SectionKeys
            .Select((key, index) =>
                requestedByKey.TryGetValue(
                    key,
                    out var selection)
                    ? selection with
                    {
                        SortOrder = selection.SortOrder < 0
                            ? index
                            : selection.SortOrder
                    }
                    : defaults[index] with
                    {
                        Included = false
                    })
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Key)
            .Select((section, index) => section with
            {
                SortOrder = index
            })
            .ToArray();

        if (!normalized.Any(section => section.Included))
        {
            throw new ReportValidationException(
                "sections",
                "Include at least one report section.");
        }

        return normalized;
    }

    private static IReadOnlyList<ReportDataSourceResponse>
        WithBaseDataSources(
            IEnumerable<ReportDataSourceResponse> sources)
    {
        return BaseDataSources
            .Concat(sources)
            .DistinctBy(source => new
            {
                source.Dataset,
                source.Version,
                source.SourceUrl
            })
            .OrderBy(source => source.Dataset)
            .ToArray();
    }

    private static bool IsIncludedByDefault(
        ReportSectionKey key,
        ReportSourceType sourceType)
    {
        return key switch
        {
            ReportSectionKey.SiteComparison =>
                sourceType == ReportSourceType.Comparison,
            ReportSectionKey.SuitabilityScore =>
                sourceType == ReportSourceType.Site,
            _ => true
        };
    }

    private static ReportBrandingRequest NormalizeBranding(
        ReportBrandingRequest? branding)
    {
        if (branding is null)
        {
            return DefaultBranding;
        }

        var organization = NormalizeLimitedText(
            branding.OrganizationName,
            "branding.organizationName",
            120,
            DefaultBranding.OrganizationName);
        var preparedBy = NormalizeLimitedText(
            branding.PreparedBy,
            "branding.preparedBy",
            120,
            DefaultBranding.PreparedBy);
        var footer = string.IsNullOrWhiteSpace(branding.FooterText)
            ? DefaultBranding.FooterText
            : NormalizeLimitedText(
                branding.FooterText,
                "branding.footerText",
                180,
                DefaultBranding.FooterText!);
        var accent = string.IsNullOrWhiteSpace(branding.AccentColor)
            ? DefaultBranding.AccentColor
            : branding.AccentColor.Trim().ToUpperInvariant();

        if (!HexColorExpression().IsMatch(accent))
        {
            throw new ReportValidationException(
                "branding.accentColor",
                "Accent color must be a six-digit hex color such as #0F766E.");
        }

        return new ReportBrandingRequest(
            organization,
            preparedBy,
            accent,
            footer);
    }

    private static string NormalizeTitle(
        string? title,
        string fallback)
    {
        return NormalizeLimitedText(
            title,
            "title",
            200,
            fallback);
    }

    private static string NormalizeLimitedText(
        string? value,
        string field,
        int maximumLength,
        string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ReportValidationException(
                field,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static void ValidateFormat(
        ReportSourceType sourceType,
        ReportFormat format)
    {
        var supported = sourceType switch
        {
            ReportSourceType.Site =>
                format is ReportFormat.Pdf
                    or ReportFormat.MetricsCsv
                    or ReportFormat.GeometryGeoJson
                    or ReportFormat.AnalysisGeoJson,
            ReportSourceType.Comparison =>
                format is ReportFormat.Pdf
                    or ReportFormat.ComparisonCsv,
            ReportSourceType.Project =>
                format == ReportFormat.ProjectArchive,
            _ => false
        };

        if (!supported)
        {
            throw new ReportValidationException(
                "format",
                $"Format '{format}' is not available for {sourceType.ToString().ToLowerInvariant()} reports.");
        }
    }

    private static async Task<IReadOnlyList<ReportResponse>>
        MapHistoryAsync(
            Task<IReadOnlyList<GeneratedReport>> reportsTask)
    {
        var reports = await reportsTask;
        return reports
            .Select(ToResponse)
            .ToArray();
    }

    private static ReportResponse ToResponse(
        GeneratedReport report)
    {
        var sections =
            JsonSerializer.Deserialize<
                IReadOnlyList<ReportSectionSelection>>(
                report.SectionsJson,
                JsonOptions) ?? [];
        var branding =
            JsonSerializer.Deserialize<ReportBrandingRequest>(
                report.BrandingJson,
                JsonOptions) ?? DefaultBranding;
        var sourceId = report.SourceType switch
        {
            ReportSourceType.Site =>
                report.SiteId ?? report.ProjectId,
            ReportSourceType.Comparison =>
                report.ComparisonId ?? report.ProjectId,
            _ => report.ProjectId
        };

        return new ReportResponse(
            report.Id,
            report.ProjectId,
            report.SourceType,
            sourceId,
            report.Format,
            report.Title,
            report.FileName,
            report.ContentType,
            report.FileSizeBytes,
            report.GeneratedAtUtc,
            sections,
            branding,
            $"/api/reports/{report.Id}/download");
    }

    [GeneratedRegex(
        "^#[0-9A-Fa-f]{6}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HexColorExpression();
}
