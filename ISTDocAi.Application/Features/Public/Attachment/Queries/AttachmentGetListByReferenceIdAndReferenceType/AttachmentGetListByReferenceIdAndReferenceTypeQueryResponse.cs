using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType
{
    public record AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse(
          string Id,
        byte[] RowVersion,
        string StoredFileName,
        string OriginalFileName,
        string FilePath,
        string? ContentType,
        string? FileExtension,
        string? FileHash,
        long FileSizeInBytes,
        string? Note,
        string ReferenceType,
        string ReferenceId, 
        string ReferenceSerial);
    
}
