using System.Xml.Linq;
namespace ISTDocAi.ArchitectureTests.TestCases;
public sealed class ProductToolsBoundaryTests
{
    [Test]
    public void Product_projects_do_not_reference_tools()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var tools = Path.Combine(root!.FullName, "tools") + Path.DirectorySeparatorChar;
        foreach (var project in Directory.EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.StartsWith(tools, StringComparison.OrdinalIgnoreCase) && !p.Contains("\\obj\\") && !p.Contains("\\bin\\")))
            foreach (var reference in XDocument.Load(project).Descendants().Attributes()
                .Where(a => a.Name.LocalName is "Include" or "Update" or "Project")
                .SelectMany(a => a.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Concat(XDocument.Load(project).Descendants("HintPath").Select(e => e.Value)))
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference));
                Assert.That(path.StartsWith(tools, StringComparison.OrdinalIgnoreCase), Is.False, project + " -> " + path);
            }
    }
}
