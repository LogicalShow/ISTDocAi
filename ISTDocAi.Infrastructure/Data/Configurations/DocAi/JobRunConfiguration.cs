using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("JobRuns", "dbo");
        builder.Property(x => x.CancelRequestedAtUtc).HasColumnOrder(16).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.MappingFingerprint).HasColumnName("MappingFingerprint").HasColumnOrder(0).HasColumnType("char(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ChangesJson).HasColumnName("ChangesJson").HasColumnOrder(1).HasColumnType("nvarchar(max)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(2).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasColumnName("Reason").HasColumnOrder(4).HasColumnType("varchar(12)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(5).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PageCount).HasColumnName("PageCount").HasColumnOrder(6).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ClientRequestId).HasColumnName("ClientRequestId").HasColumnOrder(7).HasColumnType("varchar(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CorrelationId).HasColumnName("CorrelationId").HasColumnOrder(8).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnOrder(9).HasColumnType("varchar(12)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Active')", "DF_Runs_Status");
        builder.Property(x => x.ErrorCode).HasColumnName("ErrorCode").HasColumnOrder(10).HasColumnType("varchar(50)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ErrorMessage).HasColumnName("ErrorMessage").HasColumnOrder(11).HasColumnType("nvarchar(400)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(12).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Runs_Created");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAtUtc").HasColumnOrder(13).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ResolvedDocumentTypeCode).HasColumnName("ResolvedDocumentTypeCode").HasColumnOrder(14).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ResultType).HasColumnName("ResultType").HasColumnOrder(15).HasColumnType("varchar(32)").IsRequired(false).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_Runs_ResolvedType", "(ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> 'AUTO')"));
        builder.HasKey(new[] { "JobId", "RunNo" }).HasName("PK_JobRuns").IsClustered(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_Runs_Reason", "(Reason IN ('New', 'Requeue', 'Reprocess', 'ModelChanged'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Runs_Status", "(Status IN ('Active', 'Completed', 'Failed'))"));
        builder.HasAlternateKey(new[] { "JobId", "RunNo", "ConsumerId" }).HasName("UQ_Runs_Consumer").IsClustered(false);
        builder.HasOne<Job>().WithMany().HasForeignKey(new[] { "JobId" }).HasPrincipalKey(new[] { "JobId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Runs_Jobs");
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Runs_Consumers");
        builder.HasIndex(new[] { "ConsumerId", "CreatedAtUtc" }).HasDatabaseName("IX_Runs_Consumer_Created").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "PageCount" }).HasFilter(null);
    }
}
