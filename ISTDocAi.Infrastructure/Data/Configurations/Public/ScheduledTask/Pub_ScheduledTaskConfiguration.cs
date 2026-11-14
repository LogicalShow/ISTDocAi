using ISTDocAi.Domain.Entities.Public;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.Public.ScheduledTask;

public class Pub_ScheduledTaskConfiguration : IEntityTypeConfiguration<Pub_ScheduledTask>
{
    public void Configure(EntityTypeBuilder<Pub_ScheduledTask> builder)
    {
        builder.Property(x => x.Id).HasMaxLength(16);
        builder.Property(x => x.ReferenceId).HasMaxLength(16);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LockedBy).HasMaxLength(200);

        // Relationship
        builder.HasMany(x => x.ScheduledTaskLogs)
            .WithOne(x => x.ScheduledTask)
            .HasForeignKey(x => x.ScheduledTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ======================
        // Indexes (Performance)
        // ======================

        // ✅ Prevent duplicate tasks for the same reference (WorkOrderId) as long as IsActive=1
        // Example: RFR_CLOSE_CHECK + WorkOrderId
        builder.HasIndex(x => new { x.Name, x.ReferenceId })
            .HasDatabaseName("UX_ScheduledTask_Name_ReferenceId_Active")
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [ReferenceId] IS NOT NULL");

        // ✅ One-time tasks due lookup
        builder.HasIndex(x => x.RunAtUtc)
            .HasDatabaseName("IX_ScheduledTask_OneTime_RunAtUtc")
            .HasFilter("[IsActive] = 1 AND [RunAtUtc] IS NOT NULL AND [LastExecutionTimeUtc] IS NULL");

        // ✅ Interval tasks filtering
        builder.HasIndex(x => x.LastExecutionTimeUtc)
            .HasDatabaseName("IX_ScheduledTask_Interval_LastExecution")
            .HasFilter("[IsActive] = 1 AND [IntervalMinutes] IS NOT NULL");

        // ✅ Lock performance
        builder.HasIndex(x => x.LockedUntilUtc)
            .HasDatabaseName("IX_ScheduledTask_LockedUntilUtc")
            .HasFilter("[IsActive] = 1");
    }
}
