using ISTDocAi.Domain.Dtos.Shared.Deletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentDelete
{
    public record AttachmentDeleteCommand(List<DeleteItem> Items):ICommand<AttachmentDeleteCommandResponse>;

}
