using ISTDocAi.Domain.Common;
using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Application.Common.Utilities;

public static class ChildCollectionComparer
{
    public record ChildCollectionDiff<TEntity, TDto>(
        List<TDto> ToAdd,
        List<(TEntity Existing, TDto Request)> ToUpdate,
        List<TEntity> ToDelete
    );

    /// <summary>
    /// Default: request DTO IS the comparison type.
    /// Delegates to the 3-type-param overload with toRequestCompare: x => x.
    /// </summary>
    public static ChildCollectionDiff<TEntity, TDto> Diff<TEntity, TDto>(
        IEnumerable<TEntity> existingEntities,
        List<TDto>? requestedItems,
        Func<TDto, string?> getDtoId,
        Func<TEntity, TDto> toSnapshot)
        where TEntity : Entity
    {
        return Diff<TEntity, TDto, TDto>(
            existingEntities,
            requestedItems,
            getDtoId,
            toEntityCompare: toSnapshot,
            toRequestCompare: x => x);
    }

    /// <summary>
    /// Flexible: entity and request DTO both map to a shared TCompare record for equality.
    /// Use when request DTO differs from what you want to compare.
    /// </summary>
    public static ChildCollectionDiff<TEntity, TDto> Diff<TEntity, TDto, TCompare>(
        IEnumerable<TEntity> existingEntities,
        List<TDto>? requestedItems,
        Func<TDto, string?> getDtoId,
        Func<TEntity, TCompare> toEntityCompare,
        Func<TDto, TCompare> toRequestCompare)
        where TEntity : Entity
    {
        var toAdd = new List<TDto>();
        var toUpdate = new List<(TEntity, TDto)>();
        var existing = existingEntities.ToList();

        if (requestedItems is null || requestedItems.Count == 0)
        {
            return new ChildCollectionDiff<TEntity, TDto>(toAdd, toUpdate, existing);
        }

        var existingLookup = existing.ToLookup(x => x.Id);
        var requestedIds = requestedItems.Select(getDtoId).ToHashSet();

        var toDelete = existing
            .Where(x => !requestedIds.Contains(x.Id))
            .ToList();

        foreach (var item in requestedItems)
        {
            var id = getDtoId(item);
            var matches = id is not null ? existingLookup[id].ToList() : [];

            if (matches.Count > 0)
            {
                var existingEntity = matches[0];
                var entitySnapshot = toEntityCompare(existingEntity);
                var requestSnapshot = toRequestCompare(item);

                if (!entitySnapshot.Equals(requestSnapshot))
                {
                    toUpdate.Add((existingEntity, item));
                }
            }
            else
            {
                toAdd.Add(item);
            }
        }

        return new ChildCollectionDiff<TEntity, TDto>(toAdd, toUpdate, toDelete);
    }
}
