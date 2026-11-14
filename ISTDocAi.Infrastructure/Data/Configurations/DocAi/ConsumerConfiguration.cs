using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class ConsumerConfiguration : IEntityTypeConfiguration<Consumer>
{
    public void Configure(EntityTypeBuilder<Consumer> builder)
    {
        builder.ToTable("Consumers", "dbo");
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(0).HasColumnType("smallint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.Code).HasColumnName("Code").HasColumnOrder(1).HasColumnType("varchar(30)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("Name").HasColumnOrder(2).HasColumnType("nvarchar(200)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ProductCode).HasColumnName("ProductCode").HasColumnOrder(3).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.TenantCode).HasColumnName("TenantCode").HasColumnOrder(4).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ApiKeyHash1).HasColumnName("ApiKeyHash1").HasColumnOrder(5).HasColumnType("binary(32)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ApiKeyHash2).HasColumnName("ApiKeyHash2").HasColumnOrder(6).HasColumnType("binary(32)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CallbackUrl).HasColumnName("CallbackUrl").HasColumnOrder(7).HasColumnType("nvarchar(400)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.WebhookSecretName).HasColumnName("WebhookSecretName").HasColumnOrder(8).HasColumnType("varchar(127)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CallbackCaCertName).HasColumnName("CallbackCaCertName").HasColumnOrder(9).HasColumnType("varchar(127)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.MaxPagesPerJob).HasColumnName("MaxPagesPerJob").HasColumnOrder(10).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(10)", "DF_Consumers_MaxPages");
        builder.Property(x => x.DailyPageCap).HasColumnName("DailyPageCap").HasColumnOrder(11).HasColumnType("int").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(2000)", "DF_Consumers_DailyCap");
        builder.Property(x => x.IsActive).HasColumnName("IsActive").HasColumnOrder(12).HasColumnType("bit").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(1)", "DF_Consumers_IsActive");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(13).HasColumnType("datetime2(0)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Consumers_Created");
        builder.Property(x => x.RowVersion).HasColumnName("RowVersion").HasColumnOrder(14).HasColumnType("rowversion").IsRequired(true).IsRowVersion();
        builder.Property(x => x.QuotaMode).HasColumnName("QuotaMode").HasColumnOrder(15).HasColumnType("varchar(10)").IsRequired().ValueGeneratedNever().HasDefaultValueSql("('Unlimited')", "DF_Consumers_QuotaMode");
        builder.Property(x => x.QuotaWarningPercent).HasColumnName("QuotaWarningPercent").HasColumnOrder(16).HasColumnType("tinyint").IsRequired().ValueGeneratedNever().HasDefaultValueSql("(10)", "DF_Consumers_QuotaWarning");
        builder.HasAlternateKey(new[] { "ProductCode", "TenantCode" }).HasName("UQ_Consumers_Product_Tenant").IsClustered(false);
        builder.ToTable(t => t.HasCheckConstraint("CK_Consumers_Callback", "((CallbackUrl IS NULL AND WebhookSecretName IS NULL) OR (CallbackUrl LIKE 'https://%' AND WebhookSecretName IS NOT NULL))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Consumers_CaCert", "(CallbackCaCertName IS NULL OR CallbackUrl IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Consumers_Limits", "(MaxPagesPerJob BETWEEN 1 AND 2000 AND DailyPageCap > 0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Consumers_QuotaMode", "(QuotaMode IN ('Unlimited','Prepaid'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Consumers_QuotaWarning", "(QuotaWarningPercent BETWEEN 1 AND 50)"));
        builder.HasKey(new[] { "ConsumerId" }).HasName("PK_Consumers").IsClustered(true);
        builder.HasAlternateKey(new[] { "Code" }).HasName("UQ_Consumers_Code").IsClustered(false);
        builder.HasIndex(new[] { "ApiKeyHash1" }).HasDatabaseName("UX_Consumers_Key1").IsUnique(true).IsClustered(false).HasFilter(null);
        builder.HasIndex(new[] { "ApiKeyHash2" }).HasDatabaseName("UX_Consumers_Key2").IsUnique(true).IsClustered(false).HasFilter("ApiKeyHash2 IS NOT NULL");
    }
}
