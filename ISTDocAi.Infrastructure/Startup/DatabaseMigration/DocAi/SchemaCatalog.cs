using ISTDocAi.Infrastructure.Data.DocAi;
using ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ISTDocAi.Infrastructure.Startup.DatabaseMigration.DocAi;

public sealed record SchemaCatalog(string[] Structure, string[] Security)
{
    public static SchemaCatalog Parse(string json) => JsonSerializer.Deserialize<SchemaCatalog>(json)
        ?? throw new InvalidOperationException("Invalid baseline catalog.");
    public static SchemaCatalog Baseline() => Parse(SqlBatchReader.ReadResource("InitialSchema.catalog.json"));
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static async Task<SchemaCatalog> ReadAsync(SqlConnection connection, SqlTransaction? transaction,
        int commandTimeoutSeconds, CancellationToken ct, bool fullSecurity = false)
    {
        var structure = new List<string>();
        var security = new List<string>();
        await using var command = new SqlCommand(CatalogSql, connection, transaction) { CommandTimeout = commandTimeoutSeconds };
        command.Parameters.AddWithValue("@fullSecurity", fullSecurity);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = 0;
        do
        {
            while (await reader.ReadAsync(ct))
                (result < 7 ? structure : security).Add(NormalizeCatalogRow(reader.GetString(0)));
            result++;
        } while (await reader.NextResultAsync(ct));
        return new(structure.Order(StringComparer.Ordinal).ToArray(), security.Order(StringComparer.Ordinal).ToArray());
    }
    private static string NormalizeCatalogRow(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Missing catalog row.");
        if (node is JsonObject row)
            foreach (var key in new[] { "definition", "default", "computed", "filter_definition" })
                if (row[key] is JsonValue value && value.TryGetValue<string>(out var definition))
                    row[key] = Regex.Replace(definition, @"\s+", " ").Trim();
        return node.ToJsonString();
    }

    private const string CatalogSql = """
        SELECT (SELECT s.name AS [schema],t.name,t.temporal_type,t.is_memory_optimized
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.name<>'__EFMigrationsHistory';
        SELECT (SELECT OBJECT_SCHEMA_NAME(c.object_id) AS [schema],OBJECT_NAME(c.object_id) AS [table],c.name,
          TYPE_NAME(c.user_type_id) AS [type],c.max_length,c.precision,c.scale,c.is_nullable,c.collation_name,
          c.is_identity,i.seed_value,i.increment_value,dc.definition AS [default],cc.definition AS computed,cc.is_persisted
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
        LEFT JOIN sys.identity_columns i ON i.object_id=c.object_id AND i.column_id=c.column_id
        LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
        LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
        WHERE t.name<>'__EFMigrationsHistory';
        SELECT (SELECT OBJECT_SCHEMA_NAME(k.parent_object_id) AS [schema],OBJECT_NAME(k.parent_object_id) AS [table],
          k.type,CASE WHEN k.is_system_named=0 THEN k.name END AS name,
          (SELECT c.name,ic.key_ordinal FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
           WHERE ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id ORDER BY ic.key_ordinal FOR JSON PATH) AS cols
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.key_constraints k WHERE OBJECT_NAME(k.parent_object_id)<>'__EFMigrationsHistory';
        SELECT (SELECT OBJECT_SCHEMA_NAME(f.parent_object_id) AS [schema],OBJECT_NAME(f.parent_object_id) AS [table],
          CASE WHEN f.is_system_named=0 THEN f.name END AS name,OBJECT_SCHEMA_NAME(f.referenced_object_id) AS refschema,
          OBJECT_NAME(f.referenced_object_id) AS reftable,f.delete_referential_action,f.update_referential_action,f.is_disabled,f.is_not_trusted,
          (SELECT COL_NAME(fc.parent_object_id,fc.parent_column_id) AS col,COL_NAME(fc.referenced_object_id,fc.referenced_column_id) AS refcol
           FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id FOR JSON PATH) AS cols
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES) FROM sys.foreign_keys f;
        SELECT (SELECT OBJECT_SCHEMA_NAME(c.parent_object_id) AS [schema],OBJECT_NAME(c.parent_object_id) AS [table],
          CASE WHEN c.is_system_named=0 THEN c.name END AS name,c.definition,c.is_disabled,c.is_not_trusted,c.is_not_for_replication
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES) FROM sys.check_constraints c;
        SELECT (SELECT OBJECT_SCHEMA_NAME(i.object_id) AS [schema],OBJECT_NAME(i.object_id) AS [table],
          CASE WHEN k.is_system_named=1 THEN NULL ELSE i.name END AS name,i.type,i.is_unique,i.is_primary_key,i.is_unique_constraint,
          i.filter_definition,i.is_disabled,
          (SELECT c.name,ic.key_ordinal,ic.is_descending_key,ic.is_included_column,ic.index_column_id
           FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
           WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.index_column_id FOR JSON PATH) AS cols
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id
        LEFT JOIN sys.key_constraints k ON k.parent_object_id=i.object_id AND k.unique_index_id=i.index_id
        WHERE t.name<>'__EFMigrationsHistory';
        SELECT (SELECT OBJECT_SCHEMA_NAME(o.object_id) AS [schema],o.name,o.type,m.definition,m.uses_ansi_nulls,m.uses_quoted_identifier,
          tr.is_disabled,tr.is_instead_of_trigger,tr.is_not_for_replication
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.objects o JOIN sys.sql_modules m ON m.object_id=o.object_id
        LEFT JOIN sys.triggers tr ON tr.object_id=o.object_id WHERE o.is_ms_shipped=0;
        SELECT (SELECT name,type_desc,USER_NAME(owning_principal_id) AS owner
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.database_principals WHERE type='R' AND (@fullSecurity=1 OR name LIKE N'docai[_]%');
        SELECT (SELECT USER_NAME(p.grantee_principal_id) AS grantee,USER_NAME(p.grantor_principal_id) AS grantor,
          p.class_desc,p.permission_name,p.state_desc,
          CASE WHEN p.class=1 THEN OBJECT_SCHEMA_NAME(p.major_id) WHEN p.class=3 THEN SCHEMA_NAME(p.major_id) END AS [schema],
          CASE WHEN p.class=1 THEN OBJECT_NAME(p.major_id) WHEN p.class=4 THEN USER_NAME(p.major_id) END AS objectname,
          CASE WHEN p.class=1 AND p.minor_id>0 THEN COL_NAME(p.major_id,p.minor_id) END AS col
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES)
        FROM sys.database_permissions p JOIN sys.database_principals grantee ON grantee.principal_id=p.grantee_principal_id
        WHERE (@fullSecurity=1 OR (grantee.type='R' AND grantee.name LIKE N'docai[_]%'))
          AND (p.class<>1 OR OBJECT_NAME(p.major_id)<>'__EFMigrationsHistory');
        """;
}
