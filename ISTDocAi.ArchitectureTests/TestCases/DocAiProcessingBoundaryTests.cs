using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using ISTDocAi.Application.Services.DocAi.Processing;
using ISTDocAi.Infrastructure;
using ISTDocAi.Infrastructure.BackgroundServices.DocAi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace ISTDocAi.ArchitectureTests.TestCases;

public sealed class DocAiProcessingBoundaryTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ISTDocAi.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Test]
    public void Processor_is_a_plain_scoped_service_and_only_its_host_is_registered()
    {
        Assert.That(typeof(ProcessJob).GetInterfaces().Any(i => i.Namespace?.StartsWith("MediatR", StringComparison.Ordinal) == true), Is.False);
        var services = new ServiceCollection();
        services.AddDocAiWorker(new ConfigurationBuilder().Build());
        var processor = services.Single(d => d.ServiceType == typeof(ProcessJob));
        Assert.That(processor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(processor.ImplementationType, Is.EqualTo(typeof(ProcessJob)));
        Assert.That(services.Where(d => d.ServiceType == typeof(IHostedService)).Select(d => d.ImplementationType),
            Is.EqualTo(new[] { typeof(JobProcessorService), typeof(WebhookSenderService), typeof(RetentionService), typeof(SignalsService) }));
        Assert.That(services.Any(d => d.ServiceType.FullName?.Contains("Quartz", StringComparison.Ordinal) == true ||
            d.ServiceType.Name.Contains("TaskScheduler", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void Pipelines_and_schedulers_do_not_reference_the_processor()
    {
        var root = Root();
        foreach (var folder in new[] { "ISTDocAi.Application/Common/Behaviors", "ISTDocAi.Infrastructure/Jobs" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            Assert.That(File.ReadAllText(file), Does.Not.Contain("ProcessJob"), file);
        var scheduler = Path.Combine(root, "ISTDocAi.Infrastructure/BackgroundServices");
        foreach (var file in Directory.EnumerateFiles(scheduler, "*.cs"))
            Assert.That(File.ReadAllText(file), Does.Not.Contain("ProcessJob"), file);
    }

    [Test]
    public void Processing_uses_only_injected_TimeProvider_for_time_reads()
    {
        var root = Root();
        foreach (var file in new[] { "ISTDocAi.Application/Services/DocAi/Processing/ProcessJob.cs",
            "ISTDocAi.Infrastructure/BackgroundServices/DocAi/JobProcessorService.cs",
            "ISTDocAi.Infrastructure/BackgroundServices/DocAi/WorkerCircuitBreaker.cs" })
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.That(Regex.IsMatch(text, @"\bDateTime(?:Offset)?\s*\.\s*(?:Now|UtcNow)\b"), Is.False, file);
            Assert.That(text, Does.Contain("TimeProvider clock"), file);
        }
    }

    [Test]
    public void Only_the_job_host_calls_ProcessJob_outside_tests()
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(o => unchecked((ushort)o.Value));
        var callers = new List<Type>();
        foreach (var assembly in new[] { typeof(ProcessJob).Assembly, typeof(JobProcessorService).Assembly,
            typeof(ISTDocAi.UI.Server.Controllers.DocAi.JobsController).Assembly }.Distinct())
        foreach (var type in assembly.GetTypes())
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var bytes = method.GetMethodBody()?.GetILAsByteArray();
            if (bytes is null) continue;
            for (var i = 0; i < bytes.Length;)
            {
                ushort code = bytes[i++];
                if (code == 0xfe) code = (ushort)(0xfe00 | bytes[i++]);
                var op = opcodes[code];
                if (op.OperandType == OperandType.InlineMethod)
                {
                    var target = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i), type.GetGenericArguments(), method.GetGenericArguments());
                    if (target?.DeclaringType == typeof(ProcessJob) && target.Name == nameof(ProcessJob.ExecuteAsync)) callers.Add(type);
                }
                i += op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, i),
                    _ => 4
                };
            }
        }
        Assert.That(callers, Is.Not.Empty);
        Assert.That(callers.All(t => t == typeof(JobProcessorService) || t.DeclaringType == typeof(JobProcessorService)), Is.True,
            string.Join(", ", callers.Select(t => t.FullName)));
    }
}
