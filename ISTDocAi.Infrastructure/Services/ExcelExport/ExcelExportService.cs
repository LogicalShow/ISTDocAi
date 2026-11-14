using ClosedXML.Excel;
using ISTDocAi.Application.Common.Contracts.ExcelExport;
using ISTDocAi.Application.Common.Interfaces.ExcelExport;

namespace ISTDocAi.Infrastructure.Services.ExcelExport;

public class ExcelExportService : IExcelExportService
{
    public byte[] Export<T>(IReadOnlyList<T> rows, ExcelExportOptions options)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(options.SheetName);
        var currentRow = 1;
        var columnCount = options.Columns.Count;

        if (options.ShowTitle && !string.IsNullOrWhiteSpace(options.Title))
        {
            worksheet.Cell(currentRow, 1).Value = options.Title;
            worksheet.Range(currentRow, 1, currentRow, columnCount).Merge();
            worksheet.Row(currentRow).Height = options.TitleRowHeight ?? 28;
            worksheet.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(16);
            worksheet.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            currentRow += 2;
        }

        var headerRow = currentRow;
        for (var index = 0; index < columnCount; index++)
        {
            var cell = worksheet.Cell(headerRow, index + 1);
            var column = options.Columns[index];
            cell.Value = column.Header;

            if (column.Width.HasValue)
            {
                worksheet.Column(index + 1).Width = column.Width.Value;
            }
        }

        ApplyHeaderStyle(worksheet.Range(headerRow, 1, headerRow, columnCount), options);
        ApplyColumnHeaderOverrides(worksheet, headerRow, options);

        var properties = typeof(T).GetProperties().ToDictionary(property => property.Name);
        var dataStartRow = headerRow + 1;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var excelRow = dataStartRow + rowIndex;

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var column = options.Columns[columnIndex];
                var cell = worksheet.Cell(excelRow, columnIndex + 1);
                var value = properties.TryGetValue(column.Key, out var property)
                    ? property.GetValue(rows[rowIndex])
                    : null;

                cell.Value = Normalize(value);

                if (!string.IsNullOrWhiteSpace(column.Format))
                {
                    cell.Style.NumberFormat.Format = column.Format;
                }
            }

            if (options.ZebraRows && rowIndex % 2 == 1)
            {
                worksheet.Range(excelRow, 1, excelRow, columnCount)
                    .Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            }

            if (options.DataRowHeight.HasValue)
            {
                worksheet.Row(excelRow).Height = options.DataRowHeight.Value;
            }
        }

        var lastRow = Math.Max(dataStartRow + rows.Count - 1, headerRow);
        ApplyColumnDataOverrides(worksheet, dataStartRow, lastRow, options);

        var tableRange = worksheet.Range(headerRow, 1, lastRow, columnCount);
        if (options.AddBorders)
        {
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        if (options.AutoFilter && rows.Count > 0)
        {
            tableRange.SetAutoFilter();
        }

        if (options.SumColumns is not null && options.SumColumns.Count > 0)
        {
            AddTotals(worksheet, dataStartRow, lastRow, options);
        }

        if (options.IsRtl)
        {
            worksheet.RightToLeft = true;
            worksheet.Style.Font.FontName = "Segoe UI";
        }

        if (options.FreezeHeader)
        {
            worksheet.SheetView.FreezeRows(headerRow);
        }

        return Save(workbook);
    }

    public byte[] ExportSimple<T>(
        IReadOnlyList<T> rows,
        string reportName,
        IReadOnlyDictionary<string, string>? headersMap = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(reportName);

        if (rows.Count == 0)
        {
            worksheet.Cell(1, 1).Value = "No data";
            return Save(workbook);
        }

        var properties = typeof(T).GetProperties();

        for (var column = 0; column < properties.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value =
                headersMap != null && headersMap.TryGetValue(properties[column].Name, out var header)
                    ? header
                    : properties[column].Name;

            worksheet.Cell(1, column + 1).Style.Font.Bold = true;
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < properties.Length; column++)
            {
                worksheet.Cell(row + 2, column + 1).Value =
                    Normalize(properties[column].GetValue(rows[row]));
            }
        }

        worksheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    private static void ApplyHeaderStyle(IXLRange headerRange, ExcelExportOptions options)
    {
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(options.HeaderBgColorHex);
        headerRange.Style.Font.FontColor = XLColor.FromHtml(options.HeaderFontColorHex);
        headerRange.Style.Font.Bold = options.HeaderBold;
        headerRange.Style.Alignment.Horizontal = options.IsRtl
            ? XLAlignmentHorizontalValues.Right
            : XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyColumnHeaderOverrides(IXLWorksheet worksheet, int headerRow, ExcelExportOptions options)
    {
        if (options.ColumnStylesByKey is null)
        {
            return;
        }

        for (var index = 0; index < options.Columns.Count; index++)
        {
            if (!options.ColumnStylesByKey.TryGetValue(options.Columns[index].Key, out var style))
            {
                continue;
            }

            var cell = worksheet.Cell(headerRow, index + 1);

            if (!string.IsNullOrWhiteSpace(style.HeaderBgColorHex))
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(style.HeaderBgColorHex);
            }

            if (!string.IsNullOrWhiteSpace(style.HeaderFontColorHex))
            {
                cell.Style.Font.FontColor = XLColor.FromHtml(style.HeaderFontColorHex);
            }

            if (style.HeaderBold.HasValue)
            {
                cell.Style.Font.Bold = style.HeaderBold.Value;
            }
        }
    }

    private static void ApplyColumnDataOverrides(
        IXLWorksheet worksheet,
        int dataStartRow,
        int lastRow,
        ExcelExportOptions options)
    {
        if (options.ColumnStylesByKey is null || lastRow < dataStartRow)
        {
            return;
        }

        for (var index = 0; index < options.Columns.Count; index++)
        {
            if (!options.ColumnStylesByKey.TryGetValue(options.Columns[index].Key, out var style))
            {
                continue;
            }

            var range = worksheet.Range(dataStartRow, index + 1, lastRow, index + 1);

            if (!string.IsNullOrWhiteSpace(style.BgColorHex))
            {
                range.Style.Fill.BackgroundColor = XLColor.FromHtml(style.BgColorHex);
            }

            if (!string.IsNullOrWhiteSpace(style.FontColorHex))
            {
                range.Style.Font.FontColor = XLColor.FromHtml(style.FontColorHex);
            }

            if (style.Bold.HasValue)
            {
                range.Style.Font.Bold = style.Bold.Value;
            }

            if (style.WrapText.HasValue)
            {
                range.Style.Alignment.WrapText = style.WrapText.Value;
            }

            if (!string.IsNullOrWhiteSpace(style.HorizontalAlign))
            {
                range.Style.Alignment.Horizontal = style.HorizontalAlign switch
                {
                    "Center" => XLAlignmentHorizontalValues.Center,
                    "Right" => XLAlignmentHorizontalValues.Right,
                    _ => XLAlignmentHorizontalValues.Left
                };
            }
        }
    }

    private static void AddTotals(IXLWorksheet worksheet, int dataStartRow, int lastRow, ExcelExportOptions options)
    {
        var totalRow = lastRow + 1;
        worksheet.Cell(totalRow, 1).Value = "Total";
        worksheet.Cell(totalRow, 1).Style.Font.Bold = true;

        for (var index = 0; index < options.Columns.Count; index++)
        {
            if (!options.SumColumns!.Contains(options.Columns[index].Key))
            {
                continue;
            }

            worksheet.Cell(totalRow, index + 1).FormulaA1 =
                $"SUM({worksheet.Cell(dataStartRow, index + 1).Address}:{worksheet.Cell(lastRow, index + 1).Address})";

            worksheet.Cell(totalRow, index + 1).Style.Font.Bold = true;
        }
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLCellValue Normalize(object? value)
    {
        if (value is null)
        {
            return new XLCellValue();
        }

        return value switch
        {
            Guid guid => guid.ToString(),
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => time.ToTimeSpan(),
            Enum enumValue => enumValue.ToString(),
            _ => XLCellValue.FromObject(value)
        };
    }
}
