using FormaGeo.Domain.Reports;

namespace FormaGeo.UnitTests.Reports;

public sealed class GeneratedReportTests
{
    [Fact]
    public void Create_StoresImmutableFileMetadata()
    {
        var id = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var report = GeneratedReport.Create(
            id,
            Guid.NewGuid(),
            ReportSourceType.Site,
            siteId,
            null,
            ReportFormat.Pdf,
            "Candidate feasibility report",
            "candidate-feasibility-report.pdf",
            "application/pdf",
            "ab/report.pdf",
            4096,
            "[]",
            "{}",
            DateTimeOffset.Parse(
                "2026-07-27T01:00:00Z"));

        Assert.Equal(id, report.Id);
        Assert.Equal(siteId, report.SiteId);
        Assert.Equal(4096, report.FileSizeBytes);
    }

    [Fact]
    public void Create_RejectsEmptyFiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedReport.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ReportSourceType.Project,
                null,
                null,
                ReportFormat.ProjectArchive,
                "Archive",
                "archive.zip",
                "application/zip",
                "ab/archive.zip",
                0,
                "[]",
                "{}",
                DateTimeOffset.UtcNow));
    }
}
