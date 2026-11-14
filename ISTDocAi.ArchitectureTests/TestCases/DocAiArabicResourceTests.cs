using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace ISTDocAi.ArchitectureTests.TestCases;

public sealed class DocAiArabicResourceTests
{
    [Test]
    public void Arabic_resources_have_real_letters_and_cover_every_DocAi_error_code()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ISTDocAi.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null);
        var folder = Path.Combine(root!.FullName, "ISTDocAi.Application", "Common", "Localization");
        var arabic = XDocument.Load(Path.Combine(folder, "Messages.ar.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
        foreach (var entry in arabic)
        {
            Assert.That(entry.Value, Does.Not.Contain("??"), entry.Key);
            Assert.That(entry.Value.Any(c => c is >= '\u0600' and <= '\u06ff' && char.IsLetter(c)), Is.True, entry.Key);
        }
        var codes = XDocument.Load(Path.Combine(folder, "Messages.resx")).Root!.Elements("data")
            .Select(d => (string)d.Attribute("name")!).Where(k => Regex.IsMatch(k, "^[A-Z][A-Z_]+$"));
        foreach (var code in codes)
        {
            Assert.That(arabic.ContainsKey(code), Is.True, code);
            Assert.That(arabic[code], Does.Not.Contain("?"), code);
        }
    }
}
