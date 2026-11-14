using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.ActionLog;

public sealed class Pub_ActionTypeConfiguration : IEntityTypeConfiguration<Pub_ActionType>
{
    public void Configure(EntityTypeBuilder<Pub_ActionType> builder)
    {
        builder.Property(entity => entity.Id).HasMaxLength(100);
    }
}
