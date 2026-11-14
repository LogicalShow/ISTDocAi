using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public sealed record MappingFieldRow(MappingMapKind MapKind, string CanonicalKey, string AzureName);

