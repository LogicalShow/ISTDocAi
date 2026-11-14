using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class MappingFieldConfiguration : IEntityTypeConfiguration<MappingField>
{
    public void Configure(EntityTypeBuilder<MappingField> builder)
    {
        builder.ToTable("MappingFields", "dbo");
        builder.Property(x => x.ProfileId).HasColumnName("ProfileId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.MapKind).HasColumnName("MapKind").HasColumnOrder(1).HasColumnType("varchar(12)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.CanonicalKey).HasColumnName("CanonicalKey").HasColumnOrder(2).HasColumnType("varchar(64)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.AzureName).HasColumnName("AzureName").HasColumnOrder(3).HasColumnType("varchar(128)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.HasKey(new[] { "ProfileId", "MapKind", "CanonicalKey" }).HasName("PK_MappingFields").IsClustered(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingFields_Key", "(DATALENGTH(CanonicalKey) BETWEEN 1 AND 64 AND CanonicalKey LIKE '[A-Za-z]%' AND CanonicalKey NOT LIKE '%[^A-Za-z0-9_]%')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingFields_Name", "(DATALENGTH(AzureName) BETWEEN 1 AND 128 AND AzureName NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.HasOne<MappingProfile>().WithMany().HasForeignKey(new[] { "ProfileId" }).HasPrincipalKey(new[] { "ProfileId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_MappingFields_Profile");
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingFields_Kind", "(MapKind IN ('Field','Header','Bill','Container'))"));
        builder.ToTable(t => t.HasTrigger("TR_MappingFields_Immutable"));
    }
}
