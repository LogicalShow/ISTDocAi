using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Services.DocAi.Mapping;
namespace ISTDocAi.ArchitectureTests.TestCases;
public sealed class SqlMappingMarkerTests
{
    [Test]
    public void Only_the_direct_SQL_resolver_implements_the_intake_marker()
    {
        var implementers = new[] { Assemblies.Domain, Assemblies.Application, Assemblies.Infrastructure, Assemblies.Api }
            .SelectMany(a => a.GetTypes())
            .Where(t => t != typeof(ISqlMappingProfileResolver) && typeof(ISqlMappingProfileResolver).IsAssignableFrom(t)).ToArray();
        Assert.That(implementers, Is.EquivalentTo(new[] { typeof(SqlMappingProfileResolver) }));
    }
}
