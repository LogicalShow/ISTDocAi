using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class UsageLedgerConfiguration : IEntityTypeConfiguration<UsageLedger>
{
    public void Configure(EntityTypeBuilder<UsageLedger> builder)
    {
        builder.ToTable("UsageLedger", "dbo");
        builder.Property(x => x.UsageId).HasColumnName("UsageId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(2).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.OperationId).HasColumnName("OperationId").HasColumnOrder(4).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.MeterCode).HasColumnName("MeterCode").HasColumnOrder(5).HasColumnType("varchar(30)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ModelId).HasColumnName("ModelId").HasColumnOrder(6).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pages).HasColumnName("Pages").HasColumnOrder(7).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PricePer1000).HasColumnName("PricePer1000").HasColumnOrder(8).HasColumnType("decimal(9,4)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.AzureCostUsd).HasColumnName("AzureCostUsd").HasColumnOrder(9).HasColumnType("decimal(12,4)").IsRequired(false).HasComputedColumnSql("(CONVERT(decimal(12,4), Pages * PricePer1000 / 1000))", stored: true);
        builder.Property(x => x.OccurredAtUtc).HasColumnName("OccurredAtUtc").HasColumnOrder(10).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.BillingPeriod).HasColumnName("BillingPeriod").HasColumnOrder(11).HasColumnType("char(7)").IsRequired(false).HasComputedColumnSql("(CONVERT(char(7), OccurredAtUtc, 126))", stored: true);
        builder.Property(x => x.RecordedAtUtc).HasColumnName("RecordedAtUtc").HasColumnOrder(12).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Usage_Recorded");
        builder.Property(x => x.ReversesUsageId).HasColumnName("ReversesUsageId").HasColumnOrder(13).HasColumnType("bigint").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CorrelationId).HasColumnName("CorrelationId").HasColumnOrder(14).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo" }).HasPrincipalKey(new[] { "JobId", "RunNo" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Usage_Runs");
        builder.ToTable(t => t.HasCheckConstraint("CK_Usage_Meter", "(MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Usage_Sign", "((ReversesUsageId IS NULL AND Pages > 0) OR (ReversesUsageId IS NOT NULL AND Pages < 0))"));
        builder.HasKey(new[] { "UsageId" }).HasName("PK_UsageLedger").IsClustered(true);
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Usage_Consumers");
        builder.HasOne<AzureOperation>().WithMany().HasForeignKey(new[] { "OperationId" }).HasPrincipalKey(new[] { "OperationId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Usage_Operations");
        builder.HasOne<UsageLedger>().WithMany().HasForeignKey(new[] { "ReversesUsageId" }).HasPrincipalKey(new[] { "UsageId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Usage_Reverses");
        builder.HasIndex(new[] { "OperationId" }).HasDatabaseName("UX_Usage_Operation").IsUnique(true).IsClustered(false).HasFilter("ReversesUsageId IS NULL");
        builder.HasIndex(new[] { "ReversesUsageId" }).HasDatabaseName("UX_Usage_OneReversal").IsUnique(true).IsClustered(false).HasFilter("ReversesUsageId IS NOT NULL");
        builder.HasIndex(new[] { "BillingPeriod", "ConsumerId" }).HasDatabaseName("IX_Usage_Period").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "MeterCode", "Pages", "AzureCostUsd", "JobId" }).HasFilter(null);
        builder.HasIndex(new[] { "OccurredAtUtc" }).HasDatabaseName("IX_Usage_Unpriced").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "MeterCode", "Pages" }).HasFilter("PricePer1000 IS NULL");
    }
}
