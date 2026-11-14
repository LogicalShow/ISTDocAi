namespace ISTDocAi.Domain.Dtos.Shared.Deletion;

public record DeleteCommandResponse
{
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> DeletedIds { get; init; } = [];
    public IReadOnlyList<BlockedDeleteItem> Blocked { get; set; } = [];

    public DeleteCommandResponse() { }

    public DeleteCommandResponse(string message, IReadOnlyList<string> deletedIds, IReadOnlyList<BlockedDeleteItem> blocked)
    {
        Message = message;
        DeletedIds = deletedIds;
        Blocked = blocked;
    }
}

