using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
using ISTDocAi.Infrastructure.Data.Sql.DocAi;
using ISTDocAi.Infrastructure.Configuration.DocAi;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;
// Unrelated host readiness tests model SQL returning no active extract models.
public static class MappingReadinessTestBoundary
{
    public static MappingProfileReadiness EmptyProfiles(ISqlConnectionFactory connections,
        IOptions<DocAiSqlOptions> options, TimeProvider clock, ILogger<MappingProfileReadiness> logger) =>
        new(connections, options, clock, logger,
            _ => Task.FromResult<IEnumerable<SqlMappingProfileResolver.Row>>([]));
}
