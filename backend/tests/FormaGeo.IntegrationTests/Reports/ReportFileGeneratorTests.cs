using FormaGeo.Application.Reports;
using FormaGeo.Domain.Reports;
using FormaGeo.Infrastructure.Reports;
using System.IO.Compression;

namespace FormaGeo.IntegrationTests.Reports;

public sealed class ReportFileGeneratorTests
{
    [Fact]
    public void GeneratePdf_CreatesReadableMultipageDocument()
    {
        var generator = new ReportFileGenerator();

        var file = generator.Generate(
            ReportFormat.Pdf,
            Snapshot());

        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(".pdf", file.FileName);
        Assert.True(file.Content.Length > 10_000);
        Assert.Equal(
            "%PDF-1.7",
            System.Text.Encoding.ASCII.GetString(
                file.Content,
                0,
                8));

        var outputPath = Environment.GetEnvironmentVariable(
            "FORMAGEO_TEST_PDF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, file.Content);
        }
    }

    [Fact]
    public void GenerateProjectArchive_ContainsPortableDataFiles()
    {
        var generator = new ReportFileGenerator();

        var file = generator.Generate(
            ReportFormat.ProjectArchive,
            Snapshot() with
            {
                SourceType = ReportSourceType.Project
            });

        using var stream = new MemoryStream(file.Content);
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read);
        var names = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet();

        Assert.Equal("application/zip", file.ContentType);
        Assert.Contains("project.json", names);
        Assert.Contains("sites.geojson", names);
        Assert.Contains("measurements.csv", names);
        Assert.Contains("analysis-findings.csv", names);
        Assert.Contains("analysis-results.geojson", names);
        Assert.Contains("data-sources.csv", names);
    }

    [Theory]
    [InlineData(ReportFormat.MetricsCsv, "text/csv")]
    [InlineData(
        ReportFormat.GeometryGeoJson,
        "application/geo+json")]
    [InlineData(
        ReportFormat.AnalysisGeoJson,
        "application/geo+json")]
    [InlineData(ReportFormat.ComparisonCsv, "text/csv")]
    public void GenerateDataExport_CreatesNonEmptyFile(
        ReportFormat format,
        string expectedContentType)
    {
        var generator = new ReportFileGenerator();

        var file = generator.Generate(
            format,
            Snapshot());

        Assert.StartsWith(expectedContentType, file.ContentType);
        Assert.NotEmpty(file.Content);
    }

    private static ReportPreviewResponse Snapshot()
    {
        var siteId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");
        var comparisonId = Guid.Parse(
            "22222222-2222-2222-2222-222222222222");
        var projectId = Guid.Parse(
            "33333333-3333-3333-3333-333333333333");
        var metric = new ReportScoreMetricResponse(
            "Mapped flood coverage",
            12.4m,
            "%",
            76m,
            19m,
            "Flood susceptibility demonstration areas",
            "2026.07",
            "Lower mapped coverage contributes a higher score.");
        var score = new ReportScoreResponse(
            siteId,
            "North River Candidate",
            78.4m,
            "Good",
            true,
            3,
            "Balanced development",
            DateTimeOffset.Parse("2026-07-26T09:30:00Z"),
            new[]
            {
                metric,
                metric with
                {
                    Name = "Road access",
                    RawValue = 410,
                    Unit = "m",
                    NormalizedScore = 84,
                    Contribution = 16.8m
                }
            },
            new[]
            {
                "Strong road accessibility."
            },
            new[]
            {
                "Moderate mapped flood exposure."
            },
            Array.Empty<string>());
        var sections = Enum.GetValues<ReportSectionKey>()
            .Select((key, index) =>
                new ReportPreviewSectionResponse(
                    key,
                    key.ToString(),
                    "Included test section.",
                    true,
                    index))
            .ToArray();

        return new ReportPreviewResponse(
            ReportSourceType.Comparison,
            comparisonId,
            projectId,
            "Riverside Growth Study",
            "2 candidate sites",
            "Riverside candidate site feasibility report",
            DateTimeOffset.Parse("2026-07-27T02:00:00Z"),
            new ReportBrandingRequest(
                "FormaGeo Planning Studio",
                "Spatial Advisory Team",
                "#0F766E",
                "Confidential decision-support snapshot"),
            sections,
            new[]
            {
                new ReportMapSiteResponse(
                    siteId,
                    "North River Candidate",
                    new IReadOnlyList<double>[]
                    {
                        new[] { 121.000, 14.600 },
                        new[] { 121.025, 14.605 },
                        new[] { 121.020, 14.625 },
                        new[] { 120.995, 14.620 },
                        new[] { 121.000, 14.600 }
                    }),
                new ReportMapSiteResponse(
                    Guid.Parse(
                        "44444444-4444-4444-4444-444444444444"),
                    "East Transit Candidate",
                    new IReadOnlyList<double>[]
                    {
                        new[] { 121.035, 14.590 },
                        new[] { 121.055, 14.595 },
                        new[] { 121.050, 14.615 },
                        new[] { 121.030, 14.610 },
                        new[] { 121.035, 14.590 }
                    })
            },
            new[]
            {
                new ReportMeasurementResponse(
                    siteId,
                    "North River Candidate",
                    48.72,
                    3140,
                    121.011,
                    14.612,
                    37,
                    "WGS 84 (EPSG:4326)")
            },
            new[]
            {
                new ReportFindingResponse(
                    siteId,
                    "North River Candidate",
                    "Hazards",
                    "Flood-zone intersection",
                    "Moderate",
                    "A portion of the site intersects mapped flood susceptibility.",
                    "Moderate susceptibility",
                    12.4,
                    "2.0.0"),
                new ReportFindingResponse(
                    siteId,
                    "North River Candidate",
                    "Planning",
                    "Zoning classification",
                    "Low",
                    "The dominant mapped designation supports mixed use.",
                    "Mixed use",
                    83.2,
                    "2.0.0")
            },
            score,
            new ReportComparisonResponse(
                comparisonId,
                "Balanced development",
                3,
                DateTimeOffset.Parse(
                    "2026-07-26T10:00:00Z"),
                new[]
                {
                    new ReportComparisonSiteResponse(
                        1,
                        siteId,
                        "North River Candidate",
                        78.4m,
                        "Good",
                        new[] { metric }),
                    new ReportComparisonSiteResponse(
                        2,
                        Guid.Parse(
                            "44444444-4444-4444-4444-444444444444"),
                        "East Transit Candidate",
                        71.2m,
                        "Good",
                        new[] { metric })
                },
                "Sites are ordered by overall score from highest to lowest under the same saved model."),
            new[]
            {
                new ReportDataSourceResponse(
                    "Flood susceptibility demonstration areas",
                    "FormaGeo",
                    "Flood susceptibility demonstration areas, FormaGeo.",
                    "2026.07",
                    "2026-07-26",
                    "CC0 1.0",
                    "/layers/flood-susceptibility.geojson"),
                new ReportDataSourceResponse(
                    "Land-use zoning demonstration areas",
                    "FormaGeo",
                    "Land-use zoning demonstration areas, FormaGeo.",
                    "2026.07",
                    "2026-07-26",
                    "CC0 1.0",
                    "/layers/land-use-zones.geojson")
            },
            new[]
            {
                "Synthetic demonstration data must not be used for permitting or engineering design.",
                "Mapped boundaries require field and title verification.",
                "This screening report does not replace professional advice."
            },
            Array.Empty<AnalysisGeoJsonFeature>());
    }
}
