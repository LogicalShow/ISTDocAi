using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentAdd;
using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentDelete;
using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType;
using ISTDocAi.Domain.Common.Shared.Response;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Services.Public.Attachment.Contracts;

namespace ISTDocAi.UI.Server.Services.Public.Attachment
{
    public interface IAttachmentService
    {
        Task<Result<AttachmentAddCommandResponse>> AddAsync(AttachmentAddRequest request, CancellationToken ct);
        Task<Result<AttachmentUpdateCommandResponse>> UpdateAsync(AttachmentUpdateRequest request, CancellationToken ct);

        Task<Result<AttachmentDeleteCommandResponse>> DeleteAsync(AttachmentDeleteRequest request, CancellationToken ct);

        Task<Result<AttachmentGetByIdQueryResponse>> GetByIdAsync(string id, CancellationToken ct);
        Task<Result<List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>> GetListAsync(string referenceType, string referenceId, CancellationToken ct);

        Task<Result<FileStreamResult>> GetFileAsync(string id, bool forceDownload, CancellationToken ct);


    }
}
