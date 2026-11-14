using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.ActionLog;

public sealed class Pub_ActionCategoryConfiguration : IEntityTypeConfiguration<Pub_ActionCategory>
{
    public void Configure(EntityTypeBuilder<Pub_ActionCategory> builder)
    {
        builder.Property(entity => entity.Id).HasMaxLength(100);
    }
}
