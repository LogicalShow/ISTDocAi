using MapsterMapper;
using ISTDocAi.Domain.Common.Shared.Response;

using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType
{
    public class AttachmentGetListByReferenceIdAndReferenceTypeQueryHandler(
         IRepositoryOp<Domain.Entities.Public.Pub_Attachment> repositoryOp,
        IMapper mapper
        ) : IQueryHandler<AttachmentGetListByReferenceIdAndReferenceTypeQuery, List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>
    {
        public async Task<Result<List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>> Handle(AttachmentGetListByReferenceIdAndReferenceTypeQuery request, CancellationToken cancellationToken)
        {
            var entities = await repositoryOp.GetListAsyncIncludes(
                predicate: a => a.ReferenceId == request.ReferenceId && a.ReferenceType == request.ReferenceType,
                includes: null,
                pageRowCount:int.MaxValue,
                cancellationToken: cancellationToken
                );

            var dtos = mapper.Map<List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>(entities.Items);
            return Result.Success( dtos );
        }
    }
}
