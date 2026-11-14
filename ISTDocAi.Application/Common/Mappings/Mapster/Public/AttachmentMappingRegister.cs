using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentAdd;
using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType;
using ISTDocAi.Domain.Entities.Public;
using Mapster;

namespace ISTDocAi.Application.Common.Mappings.Mapster.Public;

public sealed class AttachmentMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AttachmentAddCommand, Pub_Attachment>();
        config.NewConfig<AttachmentUpdateCommand, Pub_Attachment>();

        config.NewConfig<Pub_Attachment, AttachmentGetByIdQueryResponse>();

        config.NewConfig<Pub_Attachment, AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>();
    }
}

