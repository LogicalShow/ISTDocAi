namespace ISTDocAi.Infrastructure.Tests.DocAi;

internal static class RepositoryPaths
{
    internal static string Root
    {
        get
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "ISTDocAi.sln"))) return folder.FullName;
            throw new InvalidOperationException("ISTDocAi repository unavailable.");
        }
    }
}
