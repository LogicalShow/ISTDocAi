using System;

namespace ISTDocAi.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}