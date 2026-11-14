using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.Attachment;

public class Pub_AttachmentConfiguration : IEntityTypeConfiguration<ISTDocAi.Domain.Entities.Public.Pub_Attachment>
{
    public void Configure(EntityTypeBuilder<Pub_Attachment> builder)
    {
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.StoredFileName })
               .IsUnique();

    }
}
