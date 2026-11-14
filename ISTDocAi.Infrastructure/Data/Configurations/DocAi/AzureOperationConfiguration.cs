using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class AzureOperationConfiguration : IEntityTypeConfiguration<AzureOperation>
{
    public void Configure(EntityTypeBuilder<AzureOperation> builder)
    {
        builder.ToTable("AzureOperations", "dbo");
        builder.Property(x => x.OperationId).HasColumnName("OperationId").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(1).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(2).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasColumnName("Kind").HasColumnOrder(3).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ModelId).HasColumnName("ModelId").HasColumnOrder(4).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.State).HasColumnName("State").HasColumnOrder(5).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Sending')", "DF_Op_State");
        builder.Property(x => x.OperationLocation).HasColumnName("OperationLocation").HasColumnOrder(6).HasColumnType("nvarchar(500)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Pages).HasColumnName("Pages").HasColumnOrder(7).HasColumnType("int").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ClassName).HasColumnName("ClassName").HasColumnOrder(8).HasColumnType("varchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ClassConfidence).HasColumnName("ClassConfidence").HasColumnOrder(9).HasColumnType("decimal(4,3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.RawResultPath).HasColumnName("RawResultPath").HasColumnOrder(10).HasColumnType("varchar(400)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.AzureErrorCode).HasColumnName("AzureErrorCode").HasColumnOrder(11).HasColumnType("varchar(50)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.SentAtUtc).HasColumnName("SentAtUtc").HasColumnOrder(12).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Op_Sent");
        builder.Property(x => x.AcceptedAtUtc).HasColumnName("AcceptedAtUtc").HasColumnOrder(13).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAtUtc").HasColumnOrder(14).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ResultDeletedAtUtc).HasColumnName("ResultDeletedAtUtc").HasColumnOrder(15).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo" }).HasPrincipalKey(new[] { "JobId", "RunNo" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Op_Run");
        builder.ToTable(t => t.HasCheckConstraint("CK_Op_Kind", "(Kind IN ('classify', 'extract'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Op_State", "(State IN ('Sending', 'Accepted', 'Succeeded', 'Failed', 'Unknown'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Op_Location", "(State NOT IN ('Accepted', 'Succeeded') OR OperationLocation IS NOT NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Op_Pages", "(State <> 'Succeeded' OR Pages > 0)"));
        builder.HasKey(new[] { "OperationId" }).HasName("PK_AzureOperations").IsClustered(true);
        builder.HasOne<ModelRegistry>().WithMany().HasForeignKey(new[] { "ModelId" }).HasPrincipalKey(new[] { "ModelId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Op_Model");
        builder.HasIndex(new[] { "JobId", "RunNo", "Kind" }).HasDatabaseName("UX_Op_Open").IsUnique(true).IsClustered(false).HasFilter("State IN ('Sending', 'Accepted')");
        builder.HasIndex(new[] { "SentAtUtc" }).HasDatabaseName("IX_Op_Unknown").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "Kind" }).HasFilter("State = 'Unknown'");
    }
}
