using System.Reflection;
using System.Runtime.CompilerServices;
using ISTDocAi.Domain.Entities.DocAi;
using NUnit.Framework;

namespace ISTDocAi.Domain.Tests;

public sealed class DocAiEntityShapeTests
{
    [Test]
    public void Every_DocAi_entity_contains_only_data_and_compiler_generated_members()
    {
        var types = typeof(Job).Assembly.GetTypes()
            .Where(t => t.Namespace is "ISTDocAi.Domain.Entities.DocAi" or
                "ISTDocAi.Domain.Common.Shared.DocAi.Snapshots").ToArray();
        Assert.That(types, Has.Length.EqualTo(25));
        foreach (var type in types)
        {
            var accessors = type.GetProperties(BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .SelectMany(p => p.GetAccessors(nonPublic: true)).ToHashSet();
            var behavior = type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.IsPublic || m.IsAssembly || m.IsFamilyOrAssembly)
                .Where(m => !accessors.Contains(m) && !m.IsDefined(typeof(CompilerGeneratedAttribute)))
                .Select(m => m.Name).ToArray();
            Assert.That(behavior, Is.Empty, $"{type.FullName} must contain data only.");
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)), "Do not inherit the template's string-key base.");
        }
    }
}
