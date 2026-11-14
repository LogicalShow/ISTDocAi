using System.Text;

namespace ISTDocAi.Infrastructure.Utilities;

/// <summary>
/// Loads SQL scripts that are compiled into the application as Embedded Resources.
/// 
/// How it works:
/// - .sql files are embedded via the .csproj: <EmbeddedResource Include="Database\**\*.sql" />
/// - At runtime we list embedded resource names from the assembly.
/// - We locate a script by matching resource name suffix (EndsWith), e.g. "usp_X.v1.sql".
/// - This avoids filesystem path issues in Docker/IIS/Azure.
/// 
/// Rules:
/// - Filenames must be globally unique (because we match by suffix).
/// - Do NOT include "GO" inside scripts (EF executes SQL as a single batch).
/// </summary>
public static class SqlScriptLoader
{
    // Cache resource names once to avoid repeated reflection calls.
    private static readonly Lazy<string[]> ResourceNames = new(() =>
        typeof(SqlScriptLoader).Assembly.GetManifestResourceNames());

    /// <summary>
    /// Loads one embedded SQL script by filename suffix.
    /// Example: LoadScript("usp_PrintRecordTicket.v2.sql")
    /// </summary>
    public static string LoadScript(string resourceEndsWith)
    {
        if (string.IsNullOrWhiteSpace(resourceEndsWith))
            throw new ArgumentException("resourceEndsWith cannot be null/empty.", nameof(resourceEndsWith));

        var names = ResourceNames.Value;

        var matches = names
            .Where(n => n.EndsWith(resourceEndsWith, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            var preview = string.Join(Environment.NewLine, names.OrderBy(n => n).Take(50));
            throw new InvalidOperationException(
                $"Embedded SQL resource not found for suffix: '{resourceEndsWith}'. " +
                $"Ensure the file is EmbeddedResource and the suffix is correct. " +
                $"First 50 embedded resources:{Environment.NewLine}{preview}");
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple embedded SQL resources match suffix '{resourceEndsWith}'. " +
                $"Make filenames unique or use a more specific suffix. Matches:{Environment.NewLine}" +
                string.Join(Environment.NewLine, matches.OrderBy(m => m)));
        }

        var assembly = typeof(SqlScriptLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(matches[0]);
        if (stream is null)
            throw new InvalidOperationException($"Resource stream could not be opened: '{matches[0]}'.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
