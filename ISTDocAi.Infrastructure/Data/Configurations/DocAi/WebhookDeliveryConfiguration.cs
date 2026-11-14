using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries", "dbo");
        builder.Property(x => x.DeliveryId).HasColumnName("DeliveryId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.EventId).HasColumnName("EventId").HasColumnOrder(1).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(2).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(4).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.EventType).HasColumnName("EventType").HasColumnOrder(5).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnOrder(6).HasColumnType("varchar(12)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Pending')", "DF_Webhook_Status");
        builder.Property(x => x.PayloadJson).HasColumnName("PayloadJson").HasColumnOrder(7).HasColumnType("nvarchar(max)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Attempts).HasColumnName("Attempts").HasColumnOrder(8).HasColumnType("tinyint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_Webhook_Attempts");
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("NextAttemptAtUtc").HasColumnOrder(9).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Webhook_Next");
        builder.Property(x => x.LeaseToken).HasColumnName("LeaseToken").HasColumnOrder(10).HasColumnType("uniqueidentifier").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.LeaseExpiresAtUtc).HasColumnName("LeaseExpiresAtUtc").HasColumnOrder(11).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.LastStatusCode).HasColumnName("LastStatusCode").HasColumnOrder(12).HasColumnType("smallint").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.LastError).HasColumnName("LastError").HasColumnOrder(13).HasColumnType("nvarchar(500)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(14).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Webhook_Created");
        builder.Property(x => x.DeliveredAtUtc).HasColumnName("DeliveredAtUtc").HasColumnOrder(15).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo" }).HasPrincipalKey(new[] { "JobId", "RunNo" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Webhook_Runs");
        builder.ToTable(t => t.HasCheckConstraint("CK_Webhook_Status", "(Status IN ('Pending', 'Delivered', 'DeadLettered', 'Superseded'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Webhook_Event", "(EventType IN ('job.completed', 'job.failed'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Webhook_Json", "(ISJSON(PayloadJson) = 1)"));
        builder.HasAlternateKey(new[] { "JobId", "RunNo", "EventType" }).HasName("UQ_Webhook_Run_Event").IsClustered(false);
        builder.HasKey(new[] { "DeliveryId" }).HasName("PK_WebhookDeliveries").IsClustered(true);
        builder.HasAlternateKey(new[] { "EventId" }).HasName("UQ_Webhook_EventId").IsClustered(false);
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Webhook_Consumers");
        builder.HasIndex(new[] { "NextAttemptAtUtc" }).HasDatabaseName("IX_Webhook_Due").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "LeaseExpiresAtUtc" }).HasFilter("Status = 'Pending'");
    }
}
