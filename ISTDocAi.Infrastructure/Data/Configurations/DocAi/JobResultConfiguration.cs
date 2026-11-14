using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class JobResultConfiguration : IEntityTypeConfiguration<JobResult>
{
    public void Configure(EntityTypeBuilder<JobResult> builder)
    {
        builder.ToTable("JobResults", "dbo");
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(0).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ContractVersion).HasColumnName("ContractVersion").HasColumnOrder(2).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ResultJson).HasColumnName("ResultJson").HasColumnOrder(3).HasColumnType("nvarchar(max)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.FlaggedFields).HasColumnName("FlaggedFields").HasColumnOrder(4).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(5).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_JobResults_Created");
        builder.HasKey(new[] { "JobId", "RunNo" }).HasName("PK_JobResults").IsClustered(true);
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo" }).HasPrincipalKey(new[] { "JobId", "RunNo" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_JobResults_Runs");
        builder.ToTable(t => t.HasCheckConstraint("CK_JobResults_Json", "(ISJSON(ResultJson) = 1)"));
        builder.HasIndex(new[] { "CreatedAtUtc" }).HasDatabaseName("IX_JobResults_Created").IsUnique(false).IsClustered(false).HasFilter(null);
    }
}
