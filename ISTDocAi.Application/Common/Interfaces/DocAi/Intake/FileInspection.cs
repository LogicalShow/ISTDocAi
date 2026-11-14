using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
namespace ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
public sealed record FileInspection(string ContentType, string Extension, int PageCount);
