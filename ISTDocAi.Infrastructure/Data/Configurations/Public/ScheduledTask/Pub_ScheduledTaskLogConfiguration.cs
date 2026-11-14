using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.ScheduledTask;

public class Pub_ScheduledTaskLogConfiguration: IEntityTypeConfiguration<Pub_ScheduledTaskLog>
{
    public void Configure(EntityTypeBuilder<Pub_ScheduledTaskLog> builder)
    {
        builder.Property(x => x.Id).HasMaxLength(16);
        builder.Property(x => x.ScheduledTaskId).HasMaxLength(16);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(x => x.ScheduledTaskId)
            .HasDatabaseName("IX_ScheduledTaskLog_ScheduledTaskId");

        builder.HasIndex(x => x.ExecutionTimeUtc)
            .HasDatabaseName("IX_ScheduledTaskLog_ExecutionTimeUtc");
    }

}
