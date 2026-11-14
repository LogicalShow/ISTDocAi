namespace ISTDocAi.UI.Server.Services.Public.Attachment.Contracts
{
    public sealed class AttachmentAddRequest
    {
        public string OriginalFileName { get; set; } = default!;
        public string? Note { get; set; }
        public string ReferenceType { get; set; } = default!;
        public string ReferenceId { get; set; } = default!;
        public string ReferenceSerial { get; set; } = default!;
        public IFormFile File { get; set; } = default!;
    }
}
