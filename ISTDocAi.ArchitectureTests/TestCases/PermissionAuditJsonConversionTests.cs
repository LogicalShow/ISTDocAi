using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.IAM;
using ISTDocAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class PermissionAuditJsonConversionTests
{
    [Test]
    public void PermissionChangesConverter_ShouldWriteNumericEnumValues()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModelOnly;Trusted_Connection=True")
            .Options;

        using var context = new AppDbContext(options);
        var converter = context.Model
            .FindEntityType(typeof(IAM_PermissionAudit))!
            .FindProperty(nameof(IAM_PermissionAudit.PermissionChanges))!
            .GetValueConverter()!;
        var changes = new Dictionary<string, PermissionChangeType>
        {
            ["ADM.IAM.USR.EDIT"] = PermissionChangeType.Revoked
        };

        var json = converter.ConvertToProvider(changes);

        Assert.That(json, Is.EqualTo("{\"ADM.IAM.USR.EDIT\":2}"));
    }
}
