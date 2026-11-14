using System.Collections.Immutable;
using ISTDocAi.Domain.Common.Enums.DocAi;
using ISTDocAi.Domain.Common.Shared.DocAi;
using ISTDocAi.Domain.Dtos.DocAi;

namespace ISTDocAi.Application.Services.DocAi;
internal static class ImmutableArraySearch
{
    internal static int FindIndex<T>(this ImmutableArray<T> items, Func<T, bool> predicate)
    {
        for (var i = 0; i < items.Length; i++)
            if (predicate(items[i]))
                return i;
        return -1;
    }
}
