using ISTDocAi.Domain.Dtos.Shared.Deletion;

namespace ISTDocAi.UI.Server.Services.Public.Attachment.Contracts
{
    public sealed class AttachmentDeleteRequest
    {
        public List<DeleteItem> Items { get; set; } = [];
    }
}
