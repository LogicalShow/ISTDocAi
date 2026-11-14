using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType
{
    public record AttachmentGetListByReferenceIdAndReferenceTypeQuery(string ReferenceId,
        string ReferenceType
        ) : IQuery<List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>;
   
}
