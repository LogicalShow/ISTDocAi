namespace ISTDocAi.Application.Common.Contracts.ExcelExport;

public sealed class ExcelExportOptions
{
    public string ReportName { get; init; } = "Report";

    public string SheetName { get; init; } = "Sheet1";

    public string? Title { get; init; }

    public bool ShowTitle { get; init; } = true;

    public required IReadOnlyList<ExcelColumn> Columns { get; init; }

    public double? TitleRowHeight { get; init; } = 28;

    public double? HeaderRowHeight { get; init; } = 20;

    public double? DataRowHeight { get; init; }

    public string HeaderBgColorHex { get; init; } = "#1F4E79";

    public string HeaderFontColorHex { get; init; } = "#FFFFFF";

    public bool HeaderBold { get; init; } = true;

    public bool AddBorders { get; init; } = true;

    public bool ZebraRows { get; init; } = true;

    public bool AutoFilter { get; init; } = true;

    public bool FreezeHeader { get; init; } = true;

    public IReadOnlySet<string>? FilterableColumns { get; init; }

    public IReadOnlySet<string>? SumColumns { get; init; }

    public bool IsRtl { get; init; }

    public IReadOnlyDictionary<string, ExcelColumnStyle>? ColumnStylesByKey { get; init; }
}
