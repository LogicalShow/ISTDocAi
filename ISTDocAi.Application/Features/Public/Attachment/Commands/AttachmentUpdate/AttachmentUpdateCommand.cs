using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.BaseModels;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate
{
   public record AttachmentUpdateCommand(
        string Id,
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
        string ReferenceSerial
       ) :ConcurrentCommandBase,ICommand<AttachmentUpdateCommandResponse>;
    
}
