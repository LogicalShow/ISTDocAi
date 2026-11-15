namespace ISTDocAi.ArchitectureTests.TestCases;
public sealed class DocAiAzureBoundaryTests
{
    [Test]
    public void Azure_and_ProtectedData_are_owned_only_by_Infrastructure()
    {
        foreach (var assembly in new[] { Assemblies.Domain, Assemblies.Application, Assemblies.Api })
        {
            Assert.That(assembly.GetReferencedAssemblies().Select(a => a.Name)
                .Where(n => n!.StartsWith("Azure.", StringComparison.Ordinal) || n == "System.Security.Cryptography.ProtectedData"), Is.Empty);
            Assert.That(Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny("Azure", "System.Security.Cryptography.ProtectedData").GetResult().IsSuccessful, Is.True);
        }
    }
    [Test]
    public void Azure_package_references_are_owned_only_by_Infrastructure()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        foreach (var path in Directory.EnumerateFiles(root!.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(root!.FullName, p).Replace('\\', '/').StartsWith("tools/", StringComparison.OrdinalIgnoreCase) && !p.Contains(".Tests") && !p.Contains("ArchitectureTests") && !p.Contains("\\obj\\") && !p.Contains("\\bin\\") && Path.GetFileNameWithoutExtension(p) != "ISTDocAi.Infrastructure"))
        {
            var references = System.Xml.Linq.XDocument.Load(path).Descendants("PackageReference").Select(e => (string?)e.Attribute("Include"));
            Assert.That(references.Where(n => n?.StartsWith("Azure.", StringComparison.Ordinal) == true || n == "System.Security.Cryptography.ProtectedData"), Is.Empty, path);
        }
    }
    [Test]
    public void Customer_contracts_have_no_cost_members()
    {
        foreach (var type in Assemblies.Application.GetTypes().Where(t => t.Namespace?.StartsWith("ISTDocAi.Application.Common.Contracts.DocAi", StringComparison.Ordinal) == true))
            foreach (var member in type.GetProperties().Select(p => p.Name).Concat(type.GetFields().Select(f => f.Name)))
                Assert.That(member.Contains("Usd", StringComparison.OrdinalIgnoreCase) || member.Contains("AzureCost", StringComparison.OrdinalIgnoreCase), Is.False, type.FullName + "." + member);
    }
}
