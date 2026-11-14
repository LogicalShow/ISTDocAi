using ISTDocAi.Infrastructure.Data;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class LayerDependencyTests
{
    [Test]
    public void Domain_ShouldNotDependOnAnything()
    {
        // Act
        var result = Types.InAssembly(Assemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ISTDocAi.Application",
                "ISTDocAi.Infrastructure",
                "ISTDocAi.UI",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "MediatR",
                "FluentValidation",
                "Mapster",
                "MapsterMapper")
            .GetResult();

        var foreignReferences = Assemblies.Domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System") && n != "netstandard" && n != "mscorlib")
            .ToList();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.True, Violations(result));
            Assert.That(foreignReferences, Is.Empty, "Domain references: " + string.Join(", ", foreignReferences));
        });
    }

    [Test]
    public void Application_ShouldNotDependOnEfCore()
    {
        // Act
        var result = Types.InAssembly(Assemblies.Application)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        var efReferences = Assemblies.Application.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore"))
            .ToList();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.True, Violations(result));
            Assert.That(efReferences, Is.Empty, "Application references: " + string.Join(", ", efReferences));
        });
    }

    [Test]
    public void Api_ShouldNotUseAppDbContext()
    {
        // Act
        var result = Types.InAssembly(Assemblies.Api)
            .ShouldNot()
            .HaveDependencyOn(typeof(AppDbContext).FullName!)
            .GetResult();

        // Assert
        Assert.That(result.IsSuccessful, Is.True, Violations(result));
    }

    private static string Violations(NetArchTest.Rules.TestResult result) =>
        "Violating types:\n" + string.Join("\n", result.FailingTypeNames ?? []);
}
