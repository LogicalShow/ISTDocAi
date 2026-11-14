using ISTDocAi.Application.Common.Contracts.ExcelExport;

namespace ISTDocAi.Application.Common.Interfaces.ExcelExport;

public interface IExcelExportService
{
    byte[] Export<T>(IReadOnlyList<T> rows, ExcelExportOptions options);

    byte[] ExportSimple<T>(
        IReadOnlyList<T> rows,
        string reportName,
        IReadOnlyDictionary<string, string>? headersMap = null);
}
