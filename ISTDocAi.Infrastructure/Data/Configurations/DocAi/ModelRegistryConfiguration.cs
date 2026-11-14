using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class ModelRegistryConfiguration : IEntityTypeConfiguration<ModelRegistry>
{
    public void Configure(EntityTypeBuilder<ModelRegistry> builder)
    {
        builder.ToTable("ModelRegistry", "dbo");
        builder.Property(x => x.ModelId).HasColumnName("ModelId").HasColumnOrder(0).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.DocumentTypeCode).HasColumnName("DocumentTypeCode").HasColumnOrder(1).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ShippingLineCode).HasColumnName("ShippingLineCode").HasColumnOrder(2).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasColumnName("Kind").HasColumnOrder(3).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Version).HasColumnName("Version").HasColumnOrder(4).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnOrder(5).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Draft')", "DF_Model_Status");
        builder.Property(x => x.MinFieldConfidence).HasColumnName("MinFieldConfidence").HasColumnOrder(6).HasColumnType("decimal(4,3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0.800)", "DF_Model_MinConf");
        builder.Property(x => x.TestFieldAccuracy).HasColumnName("TestFieldAccuracy").HasColumnOrder(7).HasColumnType("decimal(5,4)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ActivatedAtUtc).HasColumnName("ActivatedAtUtc").HasColumnOrder(8).HasColumnType("datetime2(0)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.RetiredAtUtc).HasColumnName("RetiredAtUtc").HasColumnOrder(9).HasColumnType("datetime2(0)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.RowVersion).HasColumnName("RowVersion").HasColumnOrder(10).HasColumnType("rowversion").IsRequired(true).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_Model_Kind", "(Kind IN ('extract', 'classify'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Model_Status", "(Status IN ('Draft', 'Testing', 'Active', 'Retired'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Model_Line", "(Kind = 'extract' OR ShippingLineCode IS NULL)"));
        // Nullable UNIQUE constraint represented as an index by approval; future changes require hand-written SQL.
        builder.HasIndex(new[] { "DocumentTypeCode", "ShippingLineCode", "Kind", "Version" }).HasDatabaseName("UQ_Model_Version").IsUnique().IsClustered(false).HasFilter(null);
        builder.HasKey(new[] { "ModelId" }).HasName("PK_ModelRegistry").IsClustered(true);
        builder.HasIndex(new[] { "DocumentTypeCode", "ShippingLineCode", "Kind" }).HasDatabaseName("UX_Model_OneActive").IsUnique(true).IsClustered(false).HasFilter("Status = 'Active'");
    }
}
