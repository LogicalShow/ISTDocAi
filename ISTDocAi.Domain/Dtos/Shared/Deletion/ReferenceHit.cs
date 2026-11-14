namespace ISTDocAi.Domain.Dtos.Shared.Deletion;

public record ReferenceHit(string DependentEntity, string Table, string ForeignKey, int Count);

