using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class ClassifierClassConfiguration : IEntityTypeConfiguration<ClassifierClass>
{
    public void Configure(EntityTypeBuilder<ClassifierClass> builder)
    {
        builder.ToTable("ClassifierClasses", "dbo");
        builder.Property(x => x.ClassifierModelId).HasColumnName("ClassifierModelId").HasColumnOrder(0).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ClassName).HasColumnName("ClassName").HasColumnOrder(1).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ShippingLineCode).HasColumnName("ShippingLineCode").HasColumnOrder(2).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.TargetDocumentTypeCode).HasColumnName("TargetDocumentTypeCode").HasColumnOrder(3).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_Class_Target", "(TargetDocumentTypeCode IS NULL OR TargetDocumentTypeCode <> 'AUTO')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Class_TargetLine", "(TargetDocumentTypeCode IS NOT NULL OR ShippingLineCode IS NULL)"));
        builder.HasKey(new[] { "ClassifierModelId", "ClassName" }).HasName("PK_ClassifierClasses").IsClustered(true);
        builder.HasOne<ModelRegistry>().WithMany().HasForeignKey(new[] { "ClassifierModelId" }).HasPrincipalKey(new[] { "ModelId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Class_Model");
    }
}
