using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class CustomerPageChargeConfiguration : IEntityTypeConfiguration<CustomerPageCharge>
{
    public void Configure(EntityTypeBuilder<CustomerPageCharge> builder)
    {
        builder.ToTable("CustomerPageCharges", "dbo");
        builder.Property(x => x.ChargeId).HasColumnName("ChargeId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(1).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(2).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pages).HasColumnName("Pages").HasColumnOrder(4).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.UsageId).HasColumnName("UsageId").HasColumnOrder(5).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.OperationId).HasColumnName("OperationId").HasColumnOrder(6).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RecordedAtUtc).HasColumnName("RecordedAtUtc").HasColumnOrder(7).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("SYSUTCDATETIME()", "DF_PageCharge_Recorded");
        builder.HasAlternateKey(new[] { "JobId", "RunNo" }).HasName("UQ_PageCharge_Run").IsClustered(false);
        builder.HasOne<PageReservation>().WithMany().HasForeignKey(new[] { "JobId", "RunNo", "ConsumerId" }).HasPrincipalKey(new[] { "JobId", "RunNo", "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PageCharge_Reservation");
        builder.ToTable(t => t.HasCheckConstraint("CK_PageCharge_Pages", "(Pages>0)"));
        builder.HasKey(new[] { "ChargeId" }).HasName("PK_CustomerPageCharges").IsClustered(true);
        builder.HasOne<UsageLedger>().WithMany().HasForeignKey(new[] { "UsageId" }).HasPrincipalKey(new[] { "UsageId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PageCharge_Usage");
        builder.HasOne<AzureOperation>().WithMany().HasForeignKey(new[] { "OperationId" }).HasPrincipalKey(new[] { "OperationId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PageCharge_Operation");
        builder.HasIndex(new[] { "ConsumerId", "RecordedAtUtc", "ChargeId" }).HasDatabaseName("IX_PageCharges_Consumer").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "Pages", "JobId", "RunNo" }).HasFilter(null);
        builder.ToTable(t => t.HasTrigger("TR_PageCharges_AppendOnly"));
        builder.ToTable(t => t.HasTrigger("TR_PageCharge_Integrity"));
    }
}
