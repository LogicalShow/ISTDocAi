namespace ISTDocAi.Domain.Dtos.Shared.Deletion;

public record BlockedDeleteItem(
    string Id,
    string? NameEn,
    string? NameAr,
    IReadOnlyList<ReferenceHit> References);


