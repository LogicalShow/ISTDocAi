using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Migrations.DocAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;

namespace ISTDocAi.Infrastructure.Tests.DocAi;

public sealed class MigrationOperationTests
{
    [Test]
    public void Cancellation_column_operations_match_the_target_model_and_preserve_physical_append_order()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        var migration = new JobCancellation();
        var model = context.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        var entity = model.FindEntityType("ISTDocAi.Domain.Entities.DocAi.JobRun")!;
        var column = entity.FindProperty("CancelRequestedAtUtc")!;
        var add = (AddColumnOperation)migration.UpOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(add.Name, Is.EqualTo(column.Name));
            Assert.That(add.Schema, Is.EqualTo(entity.GetSchema()));
            Assert.That(add.Table, Is.EqualTo(entity.GetTableName()));
            Assert.That(add.ColumnType, Is.EqualTo(column.GetColumnType()).And.EqualTo("datetime2(3)"));
            Assert.That(add.IsNullable, Is.True);
            Assert.That(column.IsNullable, Is.True);
            Assert.That(add.ClrType, Is.EqualTo(typeof(DateTime)));
            Assert.That(column.GetColumnOrder(), Is.EqualTo(16));
            Assert.That(entity.GetProperties().Where(p => p != column).All(p => p.GetColumnOrder() < 16), Is.True);
            Assert.That(model.FindEntityType("ISTDocAi.Domain.Entities.DocAi.AzurePriceCheck"), Is.Null);
        });
        var drop = (DropColumnOperation)migration.DownOperations.Single();
        Assert.That((drop.Schema, drop.Table, drop.Name), Is.EqualTo((add.Schema, add.Table, add.Name)));
        var sql = context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, model);
        Assert.That(sql.Single().CommandText.Trim(), Is.EqualTo("ALTER TABLE [dbo].[JobRuns] ADD [CancelRequestedAtUtc] datetime2(3) NULL;"));
    }

    [Test]
    public void Price_refresh_is_pure_EF_and_role_removal_has_no_model_changes()
    {
        using var context = new DocAiDesignTimeFactory().CreateDbContext([]);
        var price = new AzurePriceRefresh();
        Assert.That(price.UpOperations.Select(o => o.GetType()), Is.EqualTo(new[] { typeof(CreateTableOperation), typeof(CreateIndexOperation) }));
        Assert.That(price.DownOperations.Single(), Is.TypeOf<DropTableOperation>());
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var before = initializer.Initialize(price.TargetModel, designTime: true).GetRelationalModel();
        var after = initializer.Initialize(new RemoveDocAiRoles().TargetModel, designTime: true).GetRelationalModel();
        Assert.That(context.GetService<IMigrationsModelDiffer>().GetDifferences(before, after), Is.Empty);
    }
}
