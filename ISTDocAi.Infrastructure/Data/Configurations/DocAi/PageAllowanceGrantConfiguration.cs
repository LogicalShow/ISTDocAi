using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class PageAllowanceGrantConfiguration : IEntityTypeConfiguration<PageAllowanceGrant>
{
    public void Configure(EntityTypeBuilder<PageAllowanceGrant> builder)
    {
        builder.ToTable("PageAllowanceGrants", "dbo");
        builder.Property(x => x.GrantId).HasColumnName("GrantId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pages).HasColumnName("Pages").HasColumnOrder(2).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasColumnName("Kind").HasColumnOrder(3).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.OriginalGrantId).HasColumnName("OriginalGrantId").HasColumnOrder(4).HasColumnType("bigint").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.OperatorIdentity).HasColumnName("OperatorIdentity").HasColumnOrder(5).HasColumnType("nvarchar(200)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Note).HasColumnName("Note").HasColumnOrder(6).HasColumnType("nvarchar(400)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RequestId).HasColumnName("RequestId").HasColumnOrder(7).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RecordedAtUtc).HasColumnName("RecordedAtUtc").HasColumnOrder(8).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("SYSUTCDATETIME()", "DF_Allowance_Recorded");
        builder.Property(x => x.BalanceAfter).HasColumnName("BalanceAfter").HasColumnOrder(9).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_Allowance_Balance");
        builder.HasAlternateKey(new[] { "GrantId", "ConsumerId" }).HasName("UQ_Allowance_Consumer").IsClustered(false);
        builder.HasOne<PageAllowanceGrant>().WithMany().HasForeignKey(new[] { "OriginalGrantId", "ConsumerId" }).HasPrincipalKey(new[] { "GrantId", "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Allowance_Original");
        builder.ToTable(t => t.HasCheckConstraint("CK_Allowance_Pages", "(Pages <> 0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Allowance_Kind", "((Kind='TopUp' AND Pages>0 AND OriginalGrantId IS NULL) OR Kind='Adjustment' OR (Kind='Reversal' AND OriginalGrantId IS NOT NULL))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Allowance_Audit", "(LEN(OperatorIdentity)>0 AND LEN(Note)>0)"));
        builder.HasKey(new[] { "GrantId" }).HasName("PK_PageAllowanceGrants").IsClustered(true);
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Allowance_Consumer");
        builder.HasAlternateKey(new[] { "RequestId" }).HasName("UQ_Allowance_Request").IsClustered(false);
        builder.HasIndex(new[] { "OriginalGrantId" }).HasDatabaseName("UX_Allowance_Reversal").IsUnique(true).IsClustered(false).HasFilter("Kind='Reversal'");
        builder.HasIndex(new[] { "ConsumerId", "RecordedAtUtc", "GrantId" }).HasDatabaseName("IX_Allowance_Consumer").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "Pages", "Kind", "BalanceAfter" }).HasFilter(null);
        builder.ToTable(t => t.HasTrigger("TR_Allowance_Insert"));
        builder.ToTable(t => t.HasTrigger("TR_Allowance_AppendOnly"));
    }
}
