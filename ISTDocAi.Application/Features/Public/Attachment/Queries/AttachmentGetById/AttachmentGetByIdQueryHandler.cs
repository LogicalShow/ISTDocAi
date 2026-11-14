using MapsterMapper;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById
{
    public class AttachmentGetByIdQueryHandler(
        IRepository<Domain.Entities.Public.Pub_Attachment> repository,
        IMapper mapper
        ) : IQueryHandler<AttachmentGetByIdQuery, AttachmentGetByIdQueryResponse>
    {
        public async Task<Result<AttachmentGetByIdQueryResponse>> Handle(AttachmentGetByIdQuery request, CancellationToken cancellationToken)
        {
           var entity = await repository.GetAsync(request.Id, null, cancellationToken:cancellationToken);

            var response = mapper.Map<AttachmentGetByIdQueryResponse>(entity);

            return Result<AttachmentGetByIdQueryResponse>.Success(response);
        }
    }
}
