using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class MappingProfileConfiguration : IEntityTypeConfiguration<MappingProfile>
{
    public void Configure(EntityTypeBuilder<MappingProfile> builder)
    {
        builder.ToTable("MappingProfiles", "dbo");
        builder.Property(x => x.ProfileId).HasColumnName("ProfileId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.ProfileCode).HasColumnName("ProfileCode").HasColumnOrder(1).HasColumnType("varchar(64)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.ProfileVersion).HasColumnName("ProfileVersion").HasColumnOrder(2).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        // BIN2 applies inside the CHECK expressions below; these columns inherit the database collation.
        builder.Property(x => x.DocumentTypeCode).HasColumnName("DocumentTypeCode").HasColumnOrder(3).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.MappingShape).HasColumnName("MappingShape").HasColumnOrder(4).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ContainersField).HasColumnName("ContainersField").HasColumnOrder(5).HasColumnType("varchar(128)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.ContainerNumberField).HasColumnName("ContainerNumberField").HasColumnOrder(6).HasColumnType("varchar(128)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.SealNumberField).HasColumnName("SealNumberField").HasColumnOrder(7).HasColumnType("varchar(128)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.BillsOfLadingField).HasColumnName("BillsOfLadingField").HasColumnOrder(8).HasColumnType("varchar(128)").IsRequired(true).UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
        builder.Property(x => x.SealedAtUtc).HasColumnName("SealedAtUtc").HasColumnOrder(9).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(10).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("SYSUTCDATETIME()", "DF_MappingProfiles_Created");
        builder.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasColumnOrder(11).HasColumnType("nvarchar(128)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("ORIGINAL_LOGIN()", "DF_MappingProfiles_By");
        builder.Property(x => x.ChangeReference).HasColumnName("ChangeReference").HasColumnOrder(12).HasColumnType("nvarchar(256)").IsRequired(true).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Fallback", "(MappingShape<>'LegacyFallback' OR DocumentTypeCode='BL')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Code", "(DATALENGTH(ProfileCode)>0 AND ProfileCode NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Reference", "(LEN(ChangeReference)>0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_ContainersField", "(DATALENGTH(ContainersField) BETWEEN 1 AND 128 AND ContainersField NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_ContainerNumberField", "(DATALENGTH(ContainerNumberField) BETWEEN 1 AND 128 AND ContainerNumberField NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_SealNumberField", "(DATALENGTH(SealNumberField) BETWEEN 1 AND 128 AND SealNumberField NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_BillsOfLadingField", "(DATALENGTH(BillsOfLadingField) BETWEEN 1 AND 128 AND BillsOfLadingField NOT LIKE '%[^A-Za-z0-9_.-]%')"));
        builder.HasKey(new[] { "ProfileId" }).HasName("PK_MappingProfiles").IsClustered(true);
        builder.HasAlternateKey(new[] { "ProfileCode" }).HasName("UQ_MappingProfiles_Code").IsClustered(false);
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Version", "(ProfileVersion>0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Type", "(DocumentTypeCode COLLATE Latin1_General_100_BIN2 IN ('BL','MANIFEST'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_MappingProfiles_Shape", "(MappingShape COLLATE Latin1_General_100_BIN2 IN ('LegacyFallback','ModelProfile'))"));
        builder.ToTable(t => t.HasTrigger("TR_MappingProfiles_Immutable"));
    }
}
