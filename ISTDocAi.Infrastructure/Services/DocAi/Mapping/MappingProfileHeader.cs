using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public sealed record MappingProfileHeader(string ModelId, string DocumentTypeCode, MappingShape MappingShape,
    string ContainersField = "containers", string ContainerNumberField = "containerNumber",
    string SealNumberField = "sealNumber", string BillsOfLadingField = "BillsOfLading");
