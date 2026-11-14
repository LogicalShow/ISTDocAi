using ISTDocAi.Domain.Entities.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.DocAi;

public sealed class PageReservationConfiguration : IEntityTypeConfiguration<PageReservation>
{
    public void Configure(EntityTypeBuilder<PageReservation> builder)
    {
        builder.ToTable("PageReservations", "dbo");
        builder.Property(x => x.JobId).HasColumnName("JobId").HasColumnOrder(0).HasColumnType("uniqueidentifier").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.RunNo).HasColumnName("RunNo").HasColumnOrder(1).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.ConsumerId).HasColumnName("ConsumerId").HasColumnOrder(2).HasColumnType("smallint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.Pages).HasColumnName("Pages").HasColumnOrder(3).HasColumnType("bigint").IsRequired(true).ValueGeneratedNever();
        builder.Property(x => x.State).HasColumnName("State").HasColumnOrder(4).HasColumnType("varchar(10)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("('Reserved')", "DF_PageReservations_State");
        builder.Property(x => x.ReservedAtUtc).HasColumnName("ReservedAtUtc").HasColumnOrder(5).HasColumnType("datetime2(3)").IsRequired(true).ValueGeneratedNever().HasDefaultValueSql("SYSUTCDATETIME()", "DF_PageReservations_Reserved");
        builder.Property(x => x.SettledAtUtc).HasColumnName("SettledAtUtc").HasColumnOrder(6).HasColumnType("datetime2(3)").IsRequired(false).ValueGeneratedNever();
        builder.Property(x => x.OperationId).HasColumnName("OperationId").HasColumnOrder(7).HasColumnType("bigint").IsRequired(false).ValueGeneratedNever();
        builder.HasKey(new[] { "JobId", "RunNo" }).HasName("PK_PageReservations").IsClustered(true);
        builder.HasAlternateKey(new[] { "JobId", "RunNo", "ConsumerId" }).HasName("UQ_Reservation_Consumer").IsClustered(false);
        builder.HasOne<JobRun>().WithMany().HasForeignKey(new[] { "JobId", "RunNo", "ConsumerId" }).HasPrincipalKey(new[] { "JobId", "RunNo", "ConsumerId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Reservation_Run");
        builder.ToTable(t => t.HasCheckConstraint("CK_Reservation_Pages", "(Pages>0)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Reservation_State", "((State='Reserved' AND SettledAtUtc IS NULL AND OperationId IS NULL) OR (State='Charged' AND SettledAtUtc IS NOT NULL AND OperationId IS NOT NULL) OR (State='Released' AND SettledAtUtc IS NOT NULL AND OperationId IS NULL))"));
        builder.HasOne<AzureOperation>().WithMany().HasForeignKey(new[] { "OperationId" }).HasPrincipalKey(new[] { "OperationId" }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Reservation_Operation");
        builder.HasIndex(new[] { "ConsumerId", "State" }).HasDatabaseName("IX_Reservations_Balance").IsUnique(false).IsClustered(false).IncludeProperties(new[] { "Pages" }).HasFilter(null);
        builder.ToTable(t => t.HasTrigger("TR_Reservation_Integrity"));
    }
}
