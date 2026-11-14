namespace ISTDocAi.UI.Server.Helpers.Models
{
    public sealed record ImageResponse
    {
        public required byte[] BinaryData { get; init; }
        public required string ContentType { get; init; }

        public required string FileName { get; init; }
    }
}
