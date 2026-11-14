using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class PageQuotaOperatorRequestConfiguration : IEntityTypeConfiguration<PageQuotaOperatorRequest>
{
    public void Configure(EntityTypeBuilder<PageQuotaOperatorRequest> builder)
    {
        // Frozen PK(RequestId), FK(ConsumerId), DEFAULT(RecordedAtUtc) are system-named.
        // Future alterations/drops require hand-written SQL resolving the real name at runtime.
        builder.ToTable("PageQuotaOperatorRequests", "dbo");
        builder.Property(x => x.RequestId).HasColumnName("RequestId").HasColumnOrder(0).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Payload).HasColumnName("Payload").HasColumnOrder(2).HasColumnType("nvarchar(1200)").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RecordedAtUtc).HasColumnName("RecordedAtUtc").HasColumnOrder(3).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("SYSUTCDATETIME()", "DF_PageQuotaOperatorRequests_RecordedAtUtc");
        builder.HasKey(new[] { "RequestId" }).HasName("PK_PageQuotaOperatorRequests").IsClustered(true);
        builder.HasOne<Consumer>().WithMany().HasForeignKey(new[] { "ConsumerId" }).HasPrincipalKey(new[] { "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PageQuotaOperatorRequests_Consumers_ConsumerId");
    }
}
