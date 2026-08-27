namespace SupportCrm.Infrastructure.Reports;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupportCrm.Application.Reports;

public class ReportExporter : IReportExporter
{
    public byte[] ExportToExcel(ReportExportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(data.Title, 31)); // Excel sheet-name limit
        for (var col = 0; col < data.Columns.Count; col++)
            sheet.Cell(1, col + 1).Value = data.Columns[col];
        sheet.Row(1).Style.Font.Bold = true;

        for (var row = 0; row < data.Rows.Count; row++)
            for (var col = 0; col < data.Rows[row].Count; col++)
                sheet.Cell(row + 2, col + 1).Value = data.Rows[row][col];

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportToPdf(ReportExportData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text(data.Title).FontSize(16).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in data.Columns) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var column in data.Columns)
                            header.Cell().Element(CellStyle).Text(column).Bold();
                    });

                    foreach (var row in data.Rows)
                        foreach (var cell in row)
                            table.Cell().Element(CellStyle).Text(cell);
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(2).PaddingHorizontal(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
