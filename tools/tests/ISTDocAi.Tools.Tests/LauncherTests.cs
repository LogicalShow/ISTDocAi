using System.ComponentModel;
using System.Diagnostics;
using ISTDocAi.Tools;
using NUnit.Framework;

namespace ISTDocAi.Tools.Tests;

public sealed class LauncherTests
{
    private string root = null!;
    private StringWriter output = null!;
    private FakeRunner runner = null!;

    [SetUp]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "ISTDocAi-launcher-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ISTDocAi.sln"), "fixture");
        output = new StringWriter();
        runner = new FakeRunner();
    }

    [TearDown]
    public void Cleanup() { output.Dispose(); Directory.Delete(root, true); }

    private Task<int> Run(string[] args, string menu = "", bool redirected = false) =>
        new Launcher(runner, new StringReader(menu), output).RunAsync(args, redirected, root);

    [TestCase("DevSetup")]
    [TestCase("ServerSetup")]
    [TestCase("KitSetup")]
    [TestCase("OperatorTool")]
    public async Task Exact_arguments_and_root(string name)
    {
        var tool = Launcher.Tools.Single(t => t.Name == name);
        CreateExe(tool);
        Assert.That(await Run(["publish", name]), Is.Zero);
        var start = runner.Calls[1];
        Assert.That(start.FileName, Is.EqualTo("dotnet"));
        Assert.That(start.WorkingDirectory, Is.EqualTo(root));
        Assert.That(start.UseShellExecute, Is.False);
        Assert.That(start.RedirectStandardOutput && start.RedirectStandardError, Is.True);
        Assert.That(start.ArgumentList, Is.EqualTo(new[] { "publish", $"tools/src/ISTDocAi.{name}", "-c", "Release", "-r", "win-x64", "--self-contained", "-p:PublishSingleFile=true", "-o", $"tools/publish/{name}" }));
        Assert.That(output.ToString(), Does.Contain("DONE: " + tool.Exe(root)));
    }

    [Test]
    public void Root_discovery_walks_both_starting_points()
    {
        var child = Directory.CreateDirectory(Path.Combine(root, "a", "b")).FullName;
        Assert.That(Launcher.FindRoot(Path.GetTempPath(), child), Is.EqualTo(root));
        Assert.That(Launcher.FindRoot(child, Path.GetTempPath()), Is.EqualTo(root));
        Assert.That(Launcher.FindRoot(Path.GetTempPath()), Is.Null);
    }

    [Test]
    public async Task Missing_root_refuses_without_process()
    {
        Assert.That(await new Launcher(runner, TextReader.Null, output).RunAsync(["publish", "all"], true, Path.GetTempPath()), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("REFUSED: ISTDocAi.sln not found"));
        Assert.That(runner.Calls, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Missing_sdk_has_clear_message(bool throws)
    {
        runner.Action = _ => throws ? throw new Win32Exception() : 1;
        Assert.That(await Run(["publish", "all"]), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain(".NET SDK unavailable"));
        Assert.That(runner.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Locked_exe_refuses()
    {
        var tool = Launcher.Tools[0]; CreateExe(tool);
        using var locked = new FileStream(tool.Exe(root), FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.That(await Run(["publish", tool.Name]), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Close it first"));
        Assert.That(runner.Calls, Has.Count.EqualTo(1));
    }

    [TestCase(0, false, 1)]
    [TestCase(7, true, 1)]
    [TestCase(0, true, 0)]
    public async Task Success_requires_exit_zero_and_exe(int code, bool exists, int expected)
    {
        if (exists) CreateExe(Launcher.Tools[0]);
        runner.Action = s => s.ArgumentList[0] == "--version" ? 0 : code;
        Assert.That(await Run(["publish", "DevSetup"]), Is.EqualTo(expected));
        if (expected != 0) Assert.That(output.ToString(), Does.Contain($"FAILED: DevSetup exit code {code}"));
    }

    [Test]
    public async Task All_continues_and_summarizes_after_failure()
    {
        foreach (var tool in Launcher.Tools) CreateExe(tool);
        runner.Action = s => s.ArgumentList.Contains("tools/src/ISTDocAi.ServerSetup") ? 9 : 0;
        Assert.That(await Run(["publish", "all"]), Is.EqualTo(1));
        Assert.That(runner.Calls, Has.Count.EqualTo(5));
        Assert.That(output.ToString(), Does.Contain("Tool         Result  Elapsed").And.Contain("FAILED: ServerSetup exit code 9").And.Contain("DONE: " + Launcher.Tools[3].Exe(root)));
    }

    [TestCase("bad\n6\n-1\n0\n")]
    [TestCase("bad\n")]
    public async Task Invalid_menu_and_eof(string menu)
    {
        Assert.That(await Run([], menu), Is.Zero);
        Assert.That(output.ToString(), Does.Contain(Launcher.Menu).And.Contain("Invalid choice"));
        Assert.That(runner.Calls, Is.Empty);
    }

    [TestCase("0\n")]
    [TestCase("")]
    public async Task Exit_and_eof_are_clean(string menu) => Assert.That(await Run([], menu), Is.Zero);

    [Test]
    public async Task Menu_start_uses_shell_and_returns_to_menu()
    {
        CreateExe(Launcher.Tools[0]);
        Assert.That(await Run([], "1\n2\n0\n"), Is.Zero);
        var start = runner.Opened.Single();
        Assert.That(start.FileName, Is.EqualTo(Launcher.Tools[0].Exe(root)));
        Assert.That(start.UseShellExecute, Is.True);
        Assert.That(start.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(start.FileName)));
    }

    [TestCase("1\n1\n0\n", false)]
    [TestCase("5\n1\n0\n", true)]
    public async Task Menu_opens_correct_folder(string menu, bool all)
    {
        foreach (var tool in Launcher.Tools) CreateExe(tool);
        Assert.That(await Run([], menu), Is.Zero);
        var start = runner.Opened.Single();
        Assert.That(start.FileName, Is.EqualTo("explorer.exe"));
        Assert.That(start.ArgumentList.Single(), Is.EqualTo(all ? Path.Combine(root, "tools", "publish") : "/select," + Launcher.Tools[0].Exe(root)));
    }

    [TestCase("--help", 0)]
    [TestCase("garbage", 2)]
    public async Task Cli_help_usage(string arg, int code)
    {
        Assert.That(await Run([arg], redirected: true), Is.EqualTo(code));
        Assert.That(output.ToString(), Does.Contain(Launcher.Usage));
        Assert.That(runner.Calls, Is.Empty);
    }

    [Test]
    public async Task Redirected_no_args_and_arbitrary_paths_are_usage()
    {
        Assert.That(await Run([], redirected: true), Is.EqualTo(2));
        Assert.That(await Run(["publish", "../other"]), Is.EqualTo(2));
        Assert.That(runner.Calls, Is.Empty);
    }

    [Test]
    public void Single_solution_contains_all_tools()
    {
        var solution = File.ReadAllText(Path.Combine(RepositoryPaths.ISTDocAiRoot, "ISTDocAi.sln"));
        foreach (var name in Launcher.Tools.Select(t => t.Name).Concat(new[] { "Provisioning", "Tools" }))
            Assert.That(solution, Does.Contain($"tools\\src\\ISTDocAi.{name}\\ISTDocAi.{name}.csproj"));
        Assert.That(solution, Does.Contain("tools\\tests\\ISTDocAi.Tools.Tests\\ISTDocAi.Tools.Tests.csproj"));
        Assert.That(File.Exists(Path.Combine(RepositoryPaths.ToolsRoot, "ISTDocAi.Tools.sln")), Is.False);
    }

    private void CreateExe(Tool tool)
    { Directory.CreateDirectory(Path.GetDirectoryName(tool.Exe(root))!); File.WriteAllText(tool.Exe(root), "fixture"); }

    private sealed class FakeRunner : IProcessRunner
    {
        public List<ProcessStartInfo> Calls { get; } = [];
        public List<ProcessStartInfo> Opened { get; } = [];
        public Func<ProcessStartInfo, int> Action { get; set; } = _ => 0;
        public Task<int> RunAsync(ProcessStartInfo start, TextWriter output)
        { Calls.Add(start); return Task.FromResult(Action(start)); }
        public void Open(ProcessStartInfo start) => Opened.Add(start);
    }
}
