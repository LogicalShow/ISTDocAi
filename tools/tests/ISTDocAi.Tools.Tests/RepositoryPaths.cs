namespace ISTDocAi.Tools.Tests;
internal static class RepositoryPaths
{
    internal static string ISTDocAiRoot => FindRoot("ISTDocAi.sln");
    internal static string ToolsRoot => Path.Combine(FindRoot("ISTDocAi.sln"), "tools");
    private static string FindRoot(string marker)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, marker))) return folder.FullName;
        throw new InvalidOperationException($"Repository marker {marker} is missing.");
    }
}
