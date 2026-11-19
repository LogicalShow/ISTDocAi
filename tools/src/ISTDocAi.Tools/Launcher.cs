using System.ComponentModel;
using System.Diagnostics;

namespace ISTDocAi.Tools;

public sealed record Tool(string Name)
{
    public string Project => $"tools/src/ISTDocAi.{Name}";
    public string Assembly => $"ISTDocAi.{Name}.exe";
    public string Exe(string root) => Path.Combine(root, "tools", "publish", Name, Assembly);
}

public sealed class Launcher(IProcessRunner runner, TextReader input, TextWriter output)
{
    public static IReadOnlyList<Tool> Tools { get; } = Array.AsReadOnly(new[]
    {
        new Tool("DevSetup"), new Tool("ServerSetup"), new Tool("KitSetup"), new Tool("OperatorTool")
    });
    public const string Usage = "ISTDocAi.Tools publish <DevSetup|ServerSetup|KitSetup|OperatorTool|all>\nISTDocAi.Tools --help";
    public const string Menu = "ISTDocAi Tools - create an exe\n1) DevSetup\n2) ServerSetup\n3) KitSetup\n4) OperatorTool\n5) All four\n0) Exit";

    public static string? FindRoot(params string[] starts)
    {
        foreach (var start in starts)
            for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "ISTDocAi.sln"))) return folder.FullName;
        return null;
    }

    public static ProcessStartInfo PublishStart(Tool tool, string root)
    {
        var start = Command("dotnet", root);
        foreach (var arg in new[] { "publish", tool.Project, "-c", "Release", "-r", "win-x64", "--self-contained",
            "-p:PublishSingleFile=true", "-o", $"tools/publish/{tool.Name}" }) start.ArgumentList.Add(arg);
        return start;
    }

    private static ProcessStartInfo Command(string file, string root) => new(file)
    {
        WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
    };

    public static ProcessStartInfo StartExe(string exe) => new(exe)
    { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! };

    public static bool IsBusy(string exe)
    {
        if (File.Exists(exe))
        {
            try { using var file = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.None); }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        return false;
    }

    public async Task<int> RunAsync(string[] args, bool redirected, params string[] starts)
    {
        if (args is ["--help"]) { output.WriteLine(Usage); return 0; }
        if (args.Length != 0 || redirected)
        {
            if (args.Length != 2 || args[0] != "publish" || !TrySelect(args[1], out var selected))
            { output.WriteLine(Usage); return 2; }
            return (await PublishAsync(selected, starts)).Success ? 0 : 1;
        }
        while (true)
        {
            output.WriteLine(Menu);
            output.Write("Choice: ");
            var choice = input.ReadLine()?.Trim();
            if (choice is null or "0") return 0;
            if (!int.TryParse(choice, out var number) || number < 1 || number > 5)
            { output.WriteLine("Invalid choice. Enter 0-5."); continue; }
            var selected = number == 5 ? Tools.ToArray() : [Tools[number - 1]];
            var result = await PublishAsync(selected, starts);
            if (result.Root is not null && (number == 5 || result.Success))
                AfterPublish(number == 5 ? Path.Combine(result.Root, "tools", "publish") : selected[0].Exe(result.Root), number == 5);
        }
    }

    private static bool TrySelect(string name, out Tool[] selected)
    {
        selected = name.Equals("all", StringComparison.OrdinalIgnoreCase) ? Tools.ToArray()
            : Tools.Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return selected.Length != 0;
    }

    private async Task<(bool Success, string? Root)> PublishAsync(Tool[] selected, string[] starts)
    {
        var root = FindRoot(starts);
        if (root is null)
        { output.WriteLine("REFUSED: ISTDocAi.sln not found. Run from a repository checkout with the .NET SDK installed."); return (false, null); }
        try
        {
            var sdk = Command("dotnet", root);
            sdk.ArgumentList.Add("--version");
            if (await runner.RunAsync(sdk, output) != 0)
            { output.WriteLine("REFUSED: .NET SDK unavailable. Install the SDK required by global.json."); return (false, null); }
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        { output.WriteLine("REFUSED: .NET SDK unavailable. Install the SDK required by global.json."); return (false, null); }
        var results = new List<(Tool Tool, bool Success, TimeSpan Elapsed)>();
        foreach (var tool in selected)
        {
            var watch = Stopwatch.StartNew();
            var success = false;
            var exe = tool.Exe(root);
            if (IsBusy(exe)) output.WriteLine($"REFUSED: {tool.Name} executable is running or locked. Close it first: {exe}");
            else
            {
                try
                {
                    var code = await runner.RunAsync(PublishStart(tool, root), output);
                    success = code == 0 && File.Exists(exe);
                    if (success) output.WriteLine($"DONE: {exe} ({watch.Elapsed})");
                    else
                    {
                        output.WriteLine($"FAILED: {tool.Name} exit code {code}");
                        if (code == 0) output.WriteLine($"Expected executable missing: {exe}");
                    }
                }
                catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
                { output.WriteLine($"FAILED: {tool.Name} could not start or complete publish ({e.GetType().Name})."); }
            }
            results.Add((tool, success, watch.Elapsed));
        }
        if (selected.Length > 1)
        {
            output.WriteLine("Tool         Result  Elapsed");
            foreach (var result in results) output.WriteLine($"{result.Tool.Name,-12} {(result.Success ? "DONE" : "FAILED"),-7} {result.Elapsed}");
        }
        return (results.All(r => r.Success), root);
    }

    private void AfterPublish(string path, bool all)
    {
        while (true)
        {
            output.WriteLine(all ? "1) Open folder  0) Back" : "1) Open folder  2) Start it  0) Back");
            output.Write("Choice: ");
            var choice = input.ReadLine()?.Trim();
            if (choice is null or "0") return;
            try
            {
                if (choice == "1")
                {
                    var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                    start.ArgumentList.Add(all ? path : $"/select,{path}");
                    runner.Open(start);
                }
                else if (choice == "2" && !all) runner.Open(StartExe(path));
                else { output.WriteLine("Invalid choice."); continue; }
                return;
            }
            catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
            { output.WriteLine("REFUSED: Could not open the folder or executable. Check Windows permissions or UAC."); }
        }
    }
}
