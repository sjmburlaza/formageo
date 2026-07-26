using FormaGeo.Domain.Reports;

namespace FormaGeo.Application.Reports;

public interface IReportFileGenerator
{
    GeneratedReportFile Generate(
        ReportFormat format,
        ReportPreviewResponse snapshot);
}
