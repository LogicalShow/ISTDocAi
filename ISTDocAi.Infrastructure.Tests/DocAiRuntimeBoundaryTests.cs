using System.Reflection;
using System.Reflection.Emit;
using ISTDocAi.Infrastructure.Data.DocAi;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class DocAiRuntimeBoundaryTests
{
    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    [Test]
    public void Infrastructure_cannot_query_DocAi_rows_or_write_through_DocAi_context_even_via_base_context_helpers()
    {
        var violations = new List<string>();
        var contextType = typeof(DocAiDbContext);
        var accessors = contextType.GetProperties(AllMembers).SelectMany(p => p.GetAccessors(true)).ToHashSet();
        Assert.That(contextType.GetMethods(AllMembers).Where(m => !accessors.Contains(m)).Select(m => m.Name),
            Is.EquivalentTo(new[] { "OnModelCreating", "ConfigureConventions" }), "The context must expose metadata only.");
        foreach (var type in typeof(DocAiDbContext).Assembly.GetTypes())
        {
            if (type == typeof(DocAiDbContext) || type.Namespace == "ISTDocAi.Infrastructure.Data.Configurations.DocAi" ||
                type.Namespace == "ISTDocAi.Infrastructure.Migrations.DocAi") continue; // Schema metadata only.
            foreach (var field in type.GetFields(AllMembers))
                if (HasRow(field.FieldType) || field.FieldType == typeof(DbContext)) violations.Add($"{type.FullName}.{field.Name}: runtime row/base-context field");
            foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
            {
                // Prevent passing a DocAi context through a generic DbContext helper/upcast. The existing IAM
                // interceptor is the single read-only base-context consumer; its IL is checked below too.
                var audit = type.FullName == "ISTDocAi.Infrastructure.Data.Interceptors.AuditableEntityInterceptor";
                var signature = method.GetParameters().Select(p => p.ParameterType).Concat(method is MethodInfo info ? [info.ReturnType] : Type.EmptyTypes);
                var body = method.GetMethodBody();
                var types = signature.Concat(body?.LocalVariables.Select(v => v.LocalType) ?? [])
                    .Concat(type.GetFields(AllMembers).Select(f => f.FieldType)).ToArray();
                if (types.Any(HasRow)) violations.Add($"{method}: runtime DocAi row/DbSet");
                if (!audit && types.Contains(typeof(DbContext))) violations.Add($"{method}: generic base-context helper/upcast");
                var references = References(method).ToArray();
                var calls = references.OfType<MethodBase>().ToArray();
                var referenceTypes = references.SelectMany(MemberTypes).ToArray();
                if (referenceTypes.Any(HasRow)) violations.Add($"{method}: runtime DocAi row reference");
                if (!audit && references.OfType<Type>().Contains(typeof(DbContext))) violations.Add($"{method}: base-context cast/token");
                var hasDocAi = types.Concat(referenceTypes).Any(HasDocAi);
                foreach (var call in calls)
                {
                    if (HasRow(call.DeclaringType!) || GenericArguments(call).Any(HasRow)) violations.Add($"{method}: {call}");
                    if ((hasDocAi || audit) && call.DeclaringType == typeof(DbContext) &&
                        call.Name is not (".ctor" or "get_Database" or "Dispose" or "DisposeAsync" or "get_ChangeTracker"))
                        violations.Add($"{method}: forbidden context API {call.Name}");
                }
            }
        }
        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        Type type => [type],
        FieldInfo field => [field.DeclaringType!, field.FieldType],
        MethodInfo info => new[] { info.DeclaringType!, info.ReturnType }.Concat(info.GetGenericArguments()),
        MethodBase method => [method.DeclaringType!],
        _ => []
    };
    private static Type[] GenericArguments(MethodBase method) => method is MethodInfo info ? info.GetGenericArguments() : Type.EmptyTypes;
    private static bool HasRow(Type type) => type.Namespace == "ISTDocAi.Domain.Entities.DocAi" ||
        type.HasElementType && HasRow(type.GetElementType()!) || type.IsGenericType && type.GetGenericArguments().Any(HasRow);
    private static bool HasDocAi(Type type) => type == typeof(DocAiDbContext) || type.HasElementType && HasDocAi(type.GetElementType()!) ||
        type.IsGenericType && type.GetGenericArguments().Any(HasDocAi);

    private static IEnumerable<MemberInfo> References(MethodBase method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null) yield break;
        for (var offset = 0; offset < bytes.Length;)
        {
            var value = bytes[offset++] == 0xfe ? (short)(0xfe00 | bytes[offset++]) : (short)bytes[offset - 1];
            var op = Codes[value];
            if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                var token = BitConverter.ToInt32(bytes, offset);
                yield return method.Module.ResolveMember(token, method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null)!;
            }
            offset += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4
            };
        }
    }
}
