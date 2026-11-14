namespace ISTDocAi.Application.Common.Contracts.ExcelExport;

public sealed class ExcelColumn
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public double? Width { get; init; }

    public string? Format { get; init; }
}
