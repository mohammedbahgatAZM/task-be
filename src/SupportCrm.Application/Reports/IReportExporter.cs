namespace SupportCrm.Application.Reports;

public interface IReportExporter
{
    byte[] ExportToExcel(ReportExportData data);
    byte[] ExportToPdf(ReportExportData data);
}
