using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi.Runtime;

public sealed class SqlResourceParityTests
{
    [Test]
    public void Embedded_resource_bytes_and_every_label_match_the_frozen_baseline()
    {
        var assembly = typeof(SqlStatementCatalog).Assembly;
        using var resources = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/Stage5a/embedded-resources.json")));
        foreach (var resource in resources.RootElement.EnumerateArray().Where(r => r.GetProperty("assembly").GetString() == "DocAi.Infrastructure"))
        {
            using var stream = assembly.GetManifestResourceStream(resource.GetProperty("name").GetString()!)!;
            Assert.That(stream, Is.Not.Null);
            Assert.That(Convert.ToHexStringLower(SHA256.HashData(stream)), Is.EqualTo(resource.GetProperty("sha256").GetString()));
        }
        using var labels = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/Stage5a/labeled-statements.json")));
        foreach (var file in labels.RootElement.EnumerateArray().GroupBy(x => x.GetProperty("file").GetString()))
        {
            using var stream = assembly.GetManifestResourceStream(file.Key == "03_docai_statements.sql" ? "DocAi.Statements.sql" : "DocAi.Billing.sql")!;
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var matches = Regex.Matches(text, @"(?m)^-- \[([SB]\d+[a-z]?)\]");
            Assert.That(matches.Count, Is.EqualTo(file.Count()));
            for (var i = 0; i < matches.Count; i++)
            {
                var label = matches[i].Groups[1].Value;
                var slice = text[matches[i].Index..(i + 1 < matches.Count ? matches[i + 1].Index : text.Length)];
                var expected = file.Single(x => x.GetProperty("label").GetString() == label);
                Assert.That(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(slice))), Is.EqualTo(expected.GetProperty("sha256").GetString()), label);
                if (label.StartsWith('S') || label == "B9") Assert.That(new SqlStatementCatalog()[label], Is.EqualTo(slice));
            }
        }
    }
}
