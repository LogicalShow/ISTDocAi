using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using System.Text.Json;
namespace ISTDocAi.Application.Services.DocAi.Intake;
internal static class RunChangeJson
{
    internal static RunChanges? Read(string? json) => json is null ? null : JsonSerializer.Deserialize<RunChanges>(json, ContractJson.Options);
}
