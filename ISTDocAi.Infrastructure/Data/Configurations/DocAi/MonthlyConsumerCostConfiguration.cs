using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class MonthlyConsumerCostConfiguration : IEntityTypeConfiguration<MonthlyConsumerCost>
{
    public void Configure(EntityTypeBuilder<MonthlyConsumerCost> builder)
    {
        builder.ToTable("MonthlyConsumerCosts", "dbo");
        builder.Property(x => x.BillingPeriod).HasColumnName("BillingPeriod").HasColumnOrder(0).HasColumnType("char(7)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Jobs).HasColumnName("Jobs").HasColumnOrder(2).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ExtractPages).HasColumnName("ExtractPages").HasColumnOrder(3).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ClassifyPages).HasColumnName("ClassifyPages").HasColumnOrder(4).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PageShare).HasColumnName("PageShare").HasColumnOrder(5).HasColumnType("decimal(9,8)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DiExtractCostUsd).HasColumnName("DiExtractCostUsd").HasColumnOrder(6).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DiClassifyCostUsd).HasColumnName("DiClassifyCostUsd").HasColumnOrder(7).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.SharedCostUsd).HasColumnName("SharedCostUsd").HasColumnOrder(8).HasColumnType("decimal(12,2)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TotalCostUsd).HasColumnName("TotalCostUsd").HasColumnOrder(9).HasColumnType("decimal(14,2)").IsRequired(false).HasComputedColumnSql("(DiExtractCostUsd + DiClassifyCostUsd + SharedCostUsd)", stored: true);
        builder.HasKey(new[] { "BillingPeriod", "ConsumerId" }).HasName("PK_MonthlyConsumerCosts").IsClustered(true);
        builder.HasOne<BillingPeriod>().WithMany().HasForeignKey(new[] { "BillingPeriod" }).HasPrincipalKey(new[] { "Period" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MCC_Period");
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MCC_Consumers");
    }
}
