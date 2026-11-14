namespace ISTDocAi.Application.Common.Utilities;

public static class StringExtensions
{
    public static string TrimOrEmpty(this string? value) =>
        value?.Trim() ?? string.Empty;
}
