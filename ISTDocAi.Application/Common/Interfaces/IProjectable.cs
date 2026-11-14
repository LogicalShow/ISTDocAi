using System.Linq.Expressions;

namespace ISTDocAi.Application.Common.Interfaces;

public interface IProjectable<TEntity, TDto>
{
    static abstract Expression<Func<TEntity, TDto>> Selector { get; }
}
