using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
namespace ISTDocAi.Infrastructure.Services.DocAi.Mapping;

public enum MappingMapKind { Field, Header, Bill, Container }

// Structural defaults match the current options constructors. ModelId identifies the producing model;
// it is intentionally not part of the fingerprint. Rows contain COMPLETE effective dictionaries,
// including constructor defaults inherited during configuration binding, not configuration deltas.
