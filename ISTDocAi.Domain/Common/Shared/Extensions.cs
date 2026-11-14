using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Common.Shared;

public static class Extensions
{
    readonly static object _lock = new object();

    public static string GenerateEntityId()
    {
        lock (_lock)
        {
            var ticks = new DateTime(2025, 1, 1).Ticks;
            return (DateTime.UtcNow.Ticks - ticks).ToString("x");
        }

    }
    public static void SetEntityId(this Entity entity)
    {
        entity.Id = GenerateEntityId();
    }

    public static DateTime ToLocalDateTime(this DateTime datetime, int utcOffset)
    {
        return datetime.AddHours(utcOffset);
    }

}
