using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Infrastructure.Data.Repositories.DocAi;

namespace ISTDocAi.ArchitectureTests.TestCases;

public sealed class DocAiPersistenceTests
{
    [Test]
    public void Sql_client_and_Dapper_are_owned_only_by_Infrastructure()
    {
        foreach (var assembly in new[] { Assemblies.Domain, Assemblies.Application, Assemblies.Api })
        {
            Assert.That(assembly.GetReferencedAssemblies().Select(a => a.Name),
                Does.Not.Contain("Dapper").And.Not.Contain("Microsoft.Data.SqlClient"));
            Assert.That(Types.InAssembly(assembly).ShouldNot()
                .HaveDependencyOnAny("Dapper", "Microsoft.Data.SqlClient").GetResult().IsSuccessful, Is.True);
        }
        var root = FindRoot();
        foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(root, p).Replace('\\', '/').StartsWith("tools/", StringComparison.OrdinalIgnoreCase) && !p.Contains("\\obj\\") && !p.Contains("\\bin\\") && !p.Contains(".Tests")))
        {
            var references = System.Xml.Linq.XDocument.Load(project).Descendants("PackageReference")
                .Select(e => (string?)e.Attribute("Include"));
            if (Path.GetFileNameWithoutExtension(project) != "ISTDocAi.Infrastructure")
            {
                Assert.That(references, Does.Not.Contain("Dapper"), project);
                Assert.That(references, Does.Not.Contain("Microsoft.Data.SqlClient"), project);
            }
        }
    }

    [Test]
    public void Persistence_ports_and_repositories_follow_the_approved_boundary()
    {
        Type[] ports = [typeof(IJobRepository), typeof(IJobQueue), typeof(IOperationStore),
            typeof(IWebhookOutbox), typeof(IModelCatalog), typeof(IRetention), typeof(IConsumerSource),
            typeof(IUsageReader), typeof(IModelThresholdSource), typeof(ICallbackSource), typeof(ISignalReader)];
        var implementations = typeof(SqlJobRepository).Assembly.GetTypes();
        foreach (var port in ports)
        {
            Assert.That(port.Assembly, Is.SameAs(Assemblies.Application));
            Assert.That(port.Namespace, Is.EqualTo("ISTDocAi.Application.Common.Interfaces.DocAi.Persistence"));
            var repository = implementations.Where(t => !t.IsAbstract && t.IsClass && port.IsAssignableFrom(t)).ToArray();
            Assert.That(repository, Has.Length.EqualTo(1), port.Name);
            Assert.That(repository[0].Namespace, Is.EqualTo("ISTDocAi.Infrastructure.Data.Repositories.DocAi"));
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ISTDocAi.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root is missing.");
    }
}
