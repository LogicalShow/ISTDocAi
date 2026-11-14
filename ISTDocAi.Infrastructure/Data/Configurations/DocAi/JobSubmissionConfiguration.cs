using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class JobSubmissionConfiguration : IEntityTypeConfiguration<JobSubmission>
{
    public void Configure(EntityTypeBuilder<JobSubmission> builder)
    {
        builder.ToTable("JobSubmissions", "dbo");
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(0).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ClientRequestId).HasColumnName("ClientRequestId").HasColumnOrder(1).HasColumnType("varchar(100)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(2).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(3).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc").HasColumnOrder(4).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("(SYSUTCDATETIME())", "DF_Sub_Created");
        builder.HasKey(new[] { "ConsumerId", "ClientRequestId" }).HasName("PK_JobSubmissions").IsClustered(true);
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo" }).HasPrincipalKey(new[] { "JobId", "RunNo" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Sub_Runs");
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Sub_Consumers");
    }
}
