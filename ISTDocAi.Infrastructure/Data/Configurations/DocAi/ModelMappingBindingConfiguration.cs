using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class ModelMappingBindingConfiguration : IEntityTypeConfiguration<ModelMappingBinding>
{
    public void Configure(EntityTypeBuilder<ModelMappingBinding> builder)
    {
        builder.ToTable("ModelMappingBindings", "dbo");
        builder.Property(x => x.ModelId).HasColumnName("ModelId").HasColumnOrder(0).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ProfileId).HasColumnName("ProfileId").HasColumnOrder(1).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.HasKey(new[] { "ModelId" }).HasName("PK_ModelMappingBindings").IsClustered(true);
        builder.HasOne<ModelRegistry>().WithMany().HasForeignKey(new[] { "ModelId" }).HasPrincipalKey(new[] { "ModelId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ModelMappingBindings_Model");
        builder.HasOne<MappingProfile>().WithMany().HasForeignKey(new[] { "ProfileId" }).HasPrincipalKey(new[] { "ProfileId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ModelMappingBindings_Profile");
        builder.HasIndex(new[] { "ProfileId" }).HasDatabaseName("IX_ModelMappingBindings_Profile").IsUnique(false).IsClustered(false).HasFilter(null);
        builder.ToTable(t => t.HasTrigger("TR_ModelMappingBindings_Immutable"));
    }
}
