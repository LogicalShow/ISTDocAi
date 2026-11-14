using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs", "dbo");
        builder.Property(x => x.JobSeq).HasColumnName("JobSeq").HasColumnOrder(0).HasColumnType("bigint").IsRequired(true).UseIdentityColumn(1, 1);
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(1).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(2).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(1)", "DF_Jobs_RunNo");
        builder.Property(x => x.ClientRequestId).HasColumnName("ClientRequestId").HasColumnOrder(4).HasColumnType("varchar(100)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.DocumentTypeCode).HasColumnName("DocumentTypeCode").HasColumnOrder(5).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RequestedShippingLineCode).HasColumnName("RequestedShippingLineCode").HasColumnOrder(6).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.IdemLine).HasColumnName("IdemLine").HasColumnOrder(7).HasColumnType("varchar(20)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ShippingLineCode).HasColumnName("ShippingLineCode").HasColumnOrder(8).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.FileSha256).HasColumnName("FileSha256").HasColumnOrder(9).HasColumnType("binary(32)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FileName).HasColumnName("FileName").HasColumnOrder(10).HasColumnType("nvarchar(260)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ContentType).HasColumnName("ContentType").HasColumnOrder(11).HasColumnType("varchar(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FileSizeBytes).HasColumnName("FileSizeBytes").HasColumnOrder(12).HasColumnType("int").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.PageCount).HasColumnName("PageCount").HasColumnOrder(13).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.BlobPath).HasColumnName("BlobPath").HasColumnOrder(14).HasColumnType("varchar(400)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ModelId).HasColumnName("ModelId").HasColumnOrder(15).HasColumnType("varchar(64)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.Stage).HasColumnName("Stage").HasColumnOrder(16).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasColumnName("Status").HasColumnOrder(17).HasColumnType("varchar(12)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Queued')", "DF_Jobs_Status");
        builder.Property(x => x.Attempts).HasColumnName("Attempts").HasColumnOrder(18).HasColumnType("tinyint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_Jobs_Attempts");
        builder.Property(x => x.TakeoverCount).HasColumnName("TakeoverCount").HasColumnOrder(19).HasColumnType("tinyint").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_Jobs_Takeovers");
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("NextAttemptAtUtc").HasColumnOrder(20).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Jobs_Next");
        builder.Property(x => x.LeaseToken).HasColumnName("LeaseToken").HasColumnOrder(21).HasColumnType("uniqueidentifier").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.LeaseExpiresAtUtc).HasColumnName("LeaseExpiresAtUtc").HasColumnOrder(22).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ErrorCode).HasColumnName("ErrorCode").HasColumnOrder(23).HasColumnType("varchar(50)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.ErrorMessage).HasColumnName("ErrorMessage").HasColumnOrder(24).HasColumnType("nvarchar(400)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CorrelationId).HasColumnName("CorrelationId").HasColumnOrder(25).HasColumnType("varchar(64)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(26).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Jobs_Created");
        builder.Property(x => x.StartedAtUtc).HasColumnName("StartedAtUtc").HasColumnOrder(27).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.CompletedAtUtc).HasColumnName("CompletedAtUtc").HasColumnOrder(28).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.IsDeleted).HasColumnName("IsDeleted").HasColumnOrder(29).HasColumnType("bit").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(0)", "DF_Jobs_IsDeleted");
        builder.Property(x => x.RowVersion).HasColumnName("RowVersion").HasColumnOrder(30).HasColumnType("rowversion").IsRequired(true).IsRowVersion();
        builder.Property(x => x.ResolvedDocumentTypeCode).HasColumnName("ResolvedDocumentTypeCode").HasColumnOrder(31).HasColumnType("varchar(20)").IsRequired(false).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_ResolvedType", "(ResolvedDocumentTypeCode IS NULL OR ResolvedDocumentTypeCode <> 'AUTO')"));
        builder.HasKey(new[] { "JobId" }).HasName("PK_Jobs").IsClustered(false);
        builder.HasAlternateKey(new[] { "JobSeq" }).HasName("UQ_Jobs_Seq").IsClustered(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_Status", "(Status IN ('Queued', 'Processing', 'Completed', 'Failed'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_Stage", "(Stage IN ('Classify', 'Extract', 'Map'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_Attempts", "(Attempts <= 5)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_Pages", "(PageCount > 0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_RunNo", "(RunNo >= 1)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_IdemLine", "(IdemLine = COALESCE(RequestedShippingLineCode, '*'))"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Jobs_Lease", "((Status = 'Processing' AND LeaseToken IS NOT NULL) OR (Status <> 'Processing' AND LeaseToken IS NULL))"));
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Jobs_Consumers");
        builder.HasOne<ModelRegistry>().WithMany().HasForeignKey(new[] { "ModelId" }).HasPrincipalKey(new[] { "ModelId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Jobs_Model");
        builder.HasIndex(new[] { "ConsumerId", "DocumentTypeCode", "FileSha256", "IdemLine" }).HasDatabaseName("UX_Jobs_Idempotency").IsUnique(true).IsClustered(false).HasFilter("IsDeleted = 0");
        builder.HasIndex(new[] { "Status", "NextAttemptAtUtc" }).HasDatabaseName("IX_Jobs_Queue").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "LeaseExpiresAtUtc" }).HasFilter("Status IN ('Queued', 'Processing') AND IsDeleted = 0");
        builder.HasIndex(new[] { "ConsumerId", "CreatedAtUtc" }).HasDatabaseName("IX_Jobs_Consumer_Created").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "PageCount", "Status" }).HasFilter(null);
    }
}
