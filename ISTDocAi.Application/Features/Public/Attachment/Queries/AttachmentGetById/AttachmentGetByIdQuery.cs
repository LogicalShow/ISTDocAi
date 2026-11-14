using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById
{
    public record AttachmentGetByIdQuery(string Id) : IQuery<AttachmentGetByIdQueryResponse>;
   
}
