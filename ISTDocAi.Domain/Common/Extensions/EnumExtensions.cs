using System.Reflection;
using ISTDocAi.Domain.Common.Attributes;

namespace ISTDocAi.Domain.Common.Extensions;

public static class EnumExtensions
{
    public static string GetStringValue(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field == null)
            return value.ToString();

        var attribute = field.GetCustomAttribute<StringValueAttribute>();
        return attribute?.Value ?? value.ToString();
    }
}

