using System.Text.Json;
using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISTDocAi.Infrastructure.Data.Configurations.IAM;

public class IAM_PermissionAuditConfiguration : IEntityTypeConfiguration<IAM_PermissionAudit>
{
    public void Configure(EntityTypeBuilder<IAM_PermissionAudit> builder)
    {
        builder.ToTable("IAM_PermissionAudit");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.EventType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(pa => pa.TargetUserId)
            .HasMaxLength(450);

        builder.Property(pa => pa.TargetRoleId)
            .HasMaxLength(450);

        var permissionChangesComparer = new ValueComparer<Dictionary<string, PermissionChangeType>?>(
            (left, right) => DictionariesEqual(left, right),
            dictionary => DictionaryHashCode(dictionary),
            dictionary => dictionary == null
                ? null
                : dictionary.ToDictionary(entry => entry.Key, entry => entry.Value));

        builder.Property(pa => pa.PermissionChanges)
            .HasColumnName("PermissionChangesJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                changes => JsonSerializer.Serialize(changes, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<string, PermissionChangeType>>(
                    json,
                    (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(permissionChangesComparer);

        builder.Property(pa => pa.Source)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(pa => pa.SourceRoleId)
            .HasMaxLength(450);

        builder.Property(pa => pa.PerformedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(pa => pa.PerformedAt)
            .IsRequired();

        builder.HasIndex(pa => pa.TargetUserId);
        builder.HasIndex(pa => pa.TargetRoleId);
        builder.HasIndex(pa => pa.SourceRoleId);
        builder.HasIndex(pa => pa.PerformedByUserId);
        builder.HasIndex(pa => pa.PerformedAt);
    }

    private static bool DictionariesEqual(
        Dictionary<string, PermissionChangeType>? left,
        Dictionary<string, PermissionChangeType>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null || left.Count != right.Count)
            return false;

        return left.All(entry =>
            right.TryGetValue(entry.Key, out var value) && value == entry.Value);
    }

    private static int DictionaryHashCode(
        Dictionary<string, PermissionChangeType>? dictionary)
    {
        if (dictionary is null)
            return 0;

        var hash = new HashCode();
        foreach (var entry in dictionary.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            hash.Add(entry.Key, StringComparer.Ordinal);
            hash.Add(entry.Value);
        }

        return hash.ToHashCode();
    }
}
