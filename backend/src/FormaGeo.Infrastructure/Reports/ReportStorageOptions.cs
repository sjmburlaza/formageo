namespace FormaGeo.Infrastructure.Reports;

public sealed class ReportStorageOptions
{
    public const string SectionName = "ReportStorage";

    public string RootPath { get; set; } = "App_Data/reports";
}
