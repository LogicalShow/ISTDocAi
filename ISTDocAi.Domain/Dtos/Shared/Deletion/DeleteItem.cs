namespace ISTDocAi.Domain.Dtos.Shared.Deletion;

public record DeleteItem
{
    public string Id { get; set; } = default!;
    public byte[] RowVersion { get; set; } = default!;
}
