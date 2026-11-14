using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class BillingPeriodConfiguration : IEntityTypeConfiguration<BillingPeriod>
{
    public void Configure(EntityTypeBuilder<BillingPeriod> builder)
    {
        builder.ToTable("BillingPeriods", "dbo");
        builder.Property(x => x.Period).HasColumnName("BillingPeriod").HasColumnOrder(0).HasColumnType("char(7)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DiExtractBillUsd).HasColumnName("DiExtractBillUsd").HasColumnOrder(1).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DiClassifyBillUsd).HasColumnName("DiClassifyBillUsd").HasColumnOrder(2).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.AzureExtractPages).HasColumnName("AzureExtractPages").HasColumnOrder(3).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.AzureClassifyPages).HasColumnName("AzureClassifyPages").HasColumnOrder(4).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.InfraCostUsd).HasColumnName("InfraCostUsd").HasColumnOrder(5).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ModelDevCostUsd).HasColumnName("ModelDevCostUsd").HasColumnOrder(6).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_BP_ModelDev");
        builder.Property(x => x.ReconciledAtUtc).HasColumnName("ReconciledAtUtc").HasColumnOrder(7).HasColumnType("datetime2(0)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ReconciledBy).HasColumnName("ReconciledBy").HasColumnOrder(8).HasColumnType("nvarchar(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ApprovedExtractVariancePages).HasColumnName("ApprovedExtractVariancePages").HasColumnOrder(9).HasColumnType("int").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_BP_VarExtract");
        builder.Property(x => x.ApprovedClassifyVariancePages).HasColumnName("ApprovedClassifyVariancePages").HasColumnOrder(10).HasColumnType("int").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_BP_VarClassify");
        builder.Property(x => x.VarianceReason).HasColumnName("VarianceReason").HasColumnOrder(11).HasColumnType("nvarchar(400)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.VarianceApprovedBy).HasColumnName("VarianceApprovedBy").HasColumnOrder(12).HasColumnType("nvarchar(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.UnallocatedUsd).HasColumnName("UnallocatedUsd").HasColumnOrder(13).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_BP_Unallocated");
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnOrder(14).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Open')", "DF_BP_Status");
        builder.Property(x => x.ClosedAtUtc).HasColumnName("ClosedAtUtc").HasColumnOrder(15).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ClosedBy).HasColumnName("ClosedBy").HasColumnOrder(16).HasColumnType("nvarchar(100)").IsRequired(false).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_BP_Period", "(BillingPeriod LIKE '[2][0-9][0-9][0-9]-[01][0-9]')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_BP_Status", "(Status IN ('Open', 'Closed'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_BP_Closed", "(Status = 'Open' OR ReconciledAtUtc IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_BP_Variance", "((ApprovedExtractVariancePages = 0 AND ApprovedClassifyVariancePages = 0) OR (VarianceReason IS NOT NULL AND VarianceApprovedBy IS NOT NULL))"));
        builder.HasKey(new[] { "Period" }).HasName("PK_BillingPeriods").IsClustered(true);
    }
}
