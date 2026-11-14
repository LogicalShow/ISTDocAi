using System.Globalization;
using System.Text.RegularExpressions;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi.ManifestNumberParserService;
public interface IManifestNumberParserService
{
    bool TryParse(string? text, out decimal value);
}
