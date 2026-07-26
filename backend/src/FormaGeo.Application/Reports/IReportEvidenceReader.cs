namespace FormaGeo.Application.Reports;

public interface IReportEvidenceReader
{
    Task<ReportEvidenceSnapshot> GetForSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default);
}
