using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class AzureMeterPriceConfiguration : IEntityTypeConfiguration<AzureMeterPrice>
{
    public void Configure(EntityTypeBuilder<AzureMeterPrice> builder)
    {
        builder.ToTable("AzureMeterPrices", "dbo");
        builder.Property(x => x.MeterCode).HasColumnName("MeterCode").HasColumnOrder(0).HasColumnType("varchar(30)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.EffectiveFrom).HasColumnName("EffectiveFrom").HasColumnOrder(1).HasColumnType("date").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PricePer1000).HasColumnName("PricePer1000").HasColumnOrder(2).HasColumnType("decimal(9,4)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Currency).HasColumnName("Currency").HasColumnOrder(3).HasColumnType("char(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('USD')", "DF_Meter_Currency");
        builder.Property(x => x.Source).HasColumnName("Source").HasColumnOrder(4).HasColumnType("nvarchar(200)").IsRequired(true).ValueGeneratedNever();
        builder.HasKey(new[] { "MeterCode", "EffectiveFrom" }).HasName("PK_AzureMeterPrices").IsClustered(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_Meter_Code", "(MeterCode IN ('DI_CUSTOM_EXTRACT', 'DI_CUSTOM_CLASSIFY'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Meter_Price", "(PricePer1000 > 0)"));
    }
}
