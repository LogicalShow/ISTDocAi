using NUnit.Framework;
namespace ISTDocAi.Infrastructure.Tests.DocAi;
internal static class SqlTestGuard
{
    internal static void Require()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCAI_TEST_SQL")))
            Assert.Ignore("Set DOCAI_TEST_SQL to run disposable SQL Server tests.");
    }
}
