using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;
public sealed class AzurePriceCheckConfiguration : IEntityTypeConfiguration<AzurePriceCheck>
{
    public void Configure(EntityTypeBuilder<AzurePriceCheck> builder)
    {
        builder.ToTable("AzurePriceChecks", "dbo");
        builder.Property(x => x.CheckId).HasColumnName("CheckId").HasColumnOrder(0).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.MeterCode).HasColumnName("MeterCode").HasColumnOrder(1).HasColumnType("varchar(30)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.StartedAtUtc).HasColumnName("StartedAtUtc").HasColumnOrder(2).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CheckedAtUtc).HasColumnName("CheckedAtUtc").HasColumnOrder(3).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Outcome).HasColumnName("Outcome").HasColumnOrder(4).HasColumnType("varchar(16)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ReasonCode).HasColumnName("ReasonCode").HasColumnOrder(5).HasColumnType("varchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ObservedPrice).HasColumnName("ObservedPrice").HasColumnOrder(6).HasColumnType("decimal(19,8)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CurrentPrice).HasColumnName("CurrentPrice").HasColumnOrder(7).HasColumnType("decimal(9,4)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ProposedEffectiveFrom).HasColumnName("ProposedEffectiveFrom").HasColumnOrder(8).HasColumnType("date").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.MatchedRowCount).HasColumnName("MatchedRowCount").HasColumnOrder(9).HasColumnType("int").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.MeterId).HasColumnName("MeterId").HasColumnOrder(10).HasColumnType("nvarchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Currency).HasColumnName("Currency").HasColumnOrder(11).HasColumnType("nvarchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Region).HasColumnName("Region").HasColumnOrder(12).HasColumnType("nvarchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Unit).HasColumnName("Unit").HasColumnOrder(13).HasColumnType("nvarchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Type).HasColumnName("Type").HasColumnOrder(14).HasColumnType("nvarchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.TierMinimumUnits).HasColumnName("TierMinimumUnits").HasColumnOrder(15).HasColumnType("decimal(19,8)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.AzureEffectiveFrom).HasColumnName("AzureEffectiveFrom").HasColumnOrder(16).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.WarningFingerprint).HasColumnName("WarningFingerprint").HasColumnOrder(17).HasColumnType("binary(32)").IsRequired(false).ValueGeneratedNever();
        builder.HasKey(x => new { x.CheckId, x.MeterCode }).HasName("PK_AzurePriceChecks").IsClustered();
        builder.HasIndex(x => x.CheckedAtUtc).HasDatabaseName("IX_AzurePriceChecks_CheckedAtUtc").IsDescending(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_AzurePriceChecks_MeterCode", "(MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_AzurePriceChecks_Outcome", "(Outcome IN ('NoChange','Updated','Refused','Failed'))"));
    }
}
