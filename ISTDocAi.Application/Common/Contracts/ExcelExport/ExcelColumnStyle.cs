namespace ISTDocAi.Application.Common.Contracts.ExcelExport;

public sealed class ExcelColumnStyle
{
    public string? BgColorHex { get; init; }

    public string? FontColorHex { get; init; }

    public bool? Bold { get; init; }

    public bool? WrapText { get; init; }

    public string? HorizontalAlign { get; init; }

    public string? HeaderBgColorHex { get; init; }

    public string? HeaderFontColorHex { get; init; }

    public bool? HeaderBold { get; init; }
}
