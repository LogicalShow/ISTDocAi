using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public
{
    [ActionSeeder]
    public class Pub_Attachment : AuditableEntity
    {
        public string StoredFileName { get; set; } //  unique name used in the storage (OriginalFileName+unique suffix)
        public string OriginalFileName { get; set; }
        public string FilePath { get; set; }
        public string? ContentType { get; set; }
        public string? FileExtension { get; set; }
        public string? FileHash { get; set; }

        public long FileSizeInBytes { get; set; }

        public string? Note { get; set; }

        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }

        public string ReferenceSerial { get; set; } 


    }
}
