using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISTDocAi.Infrastructure.Migrations.DocAi
{
    /// <inheritdoc />
    public partial class RemoveDocAiRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @role sysname, @roleId int, @member sysname, @securable nvarchar(776), @sql nvarchar(max);
                DECLARE roles CURSOR LOCAL STATIC READ_ONLY FOR
                    SELECT name FROM sys.database_principals
                    WHERE type = 'R' AND name IN (N'docai_runtime', N'docai_operator', N'docai_quota_operator', N'docai_modelteam');
                OPEN roles;
                FETCH NEXT FROM roles INTO @role;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @roleId = DATABASE_PRINCIPAL_ID(@role);
                    DECLARE members CURSOR LOCAL STATIC READ_ONLY FOR
                        SELECT p.name FROM sys.database_role_members rm
                        JOIN sys.database_principals p ON p.principal_id = rm.member_principal_id
                        WHERE rm.role_principal_id = @roleId;
                    OPEN members;
                    FETCH NEXT FROM members INTO @member;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @sql = N'ALTER ROLE ' + QUOTENAME(@role) + N' DROP MEMBER ' + QUOTENAME(@member) + N';';
                        EXEC sys.sp_executesql @sql;
                        FETCH NEXT FROM members INTO @member;
                    END;
                    CLOSE members;
                    DEALLOCATE members;

                    -- Transfer explicit ownership before dropping the principal. Inherited schema ownership stays dbo.
                    DECLARE owned CURSOR LOCAL STATIC READ_ONLY FOR
                        SELECT N'SCHEMA::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.schemas WHERE principal_id = @roleId
                        UNION ALL SELECT N'OBJECT::' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.objects WHERE principal_id = @roleId
                        UNION ALL SELECT N'TYPE::' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.types WHERE principal_id = @roleId
                        UNION ALL SELECT N'XML SCHEMA COLLECTION::' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.xml_schema_collections WHERE principal_id = @roleId
                        UNION ALL SELECT N'ASSEMBLY::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.assemblies WHERE principal_id = @roleId
                        UNION ALL SELECT N'MESSAGE TYPE::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.service_message_types WHERE principal_id = @roleId
                        UNION ALL SELECT N'CONTRACT::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.service_contracts WHERE principal_id = @roleId
                        UNION ALL SELECT N'SERVICE::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.services WHERE principal_id = @roleId
                        UNION ALL SELECT N'REMOTE SERVICE BINDING::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.remote_service_bindings WHERE principal_id = @roleId
                        UNION ALL SELECT N'ROUTE::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.routes WHERE principal_id = @roleId
                        UNION ALL SELECT N'FULLTEXT CATALOG::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.fulltext_catalogs WHERE principal_id = @roleId
                        UNION ALL SELECT N'FULLTEXT STOPLIST::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.fulltext_stoplists WHERE principal_id = @roleId
                        UNION ALL SELECT N'SEARCH PROPERTY LIST::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.registered_search_property_lists WHERE principal_id = @roleId
                        UNION ALL SELECT N'DATABASE SCOPED CREDENTIAL::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.database_scoped_credentials WHERE principal_id = @roleId
                        UNION ALL SELECT N'CERTIFICATE::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.certificates WHERE principal_id = @roleId
                        UNION ALL SELECT N'ASYMMETRIC KEY::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.asymmetric_keys WHERE principal_id = @roleId
                        UNION ALL SELECT N'SYMMETRIC KEY::' + QUOTENAME(name) COLLATE DATABASE_DEFAULT FROM sys.symmetric_keys WHERE principal_id = @roleId
                        UNION ALL SELECT CASE type WHEN 'R' THEN N'ROLE::' ELSE N'APPLICATION ROLE::' END + QUOTENAME(name) COLLATE DATABASE_DEFAULT
                            FROM sys.database_principals WHERE owning_principal_id = @roleId AND type IN ('R', 'A');
                    OPEN owned;
                    FETCH NEXT FROM owned INTO @securable;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        SET @sql = N'ALTER AUTHORIZATION ON ' + @securable + N' TO dbo;';
                        EXEC sys.sp_executesql @sql;
                        FETCH NEXT FROM owned INTO @securable;
                    END;
                    CLOSE owned;
                    DEALLOCATE owned;
                    -- DROP ROLE also verifies no remaining ownership dependency; EF rolls back on failure.
                    SET @sql = N'DROP ROLE ' + QUOTENAME(@role) + N';';
                    EXEC sys.sp_executesql @sql;
                    FETCH NEXT FROM roles INTO @role;
                END;
                CLOSE roles;
                DEALLOCATE roles;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF DATABASE_PRINCIPAL_ID(N'docai_runtime') IS NULL CREATE ROLE docai_runtime AUTHORIZATION dbo;
                IF DATABASE_PRINCIPAL_ID(N'docai_operator') IS NULL CREATE ROLE docai_operator AUTHORIZATION dbo;
                IF DATABASE_PRINCIPAL_ID(N'docai_quota_operator') IS NULL CREATE ROLE docai_quota_operator AUTHORIZATION dbo;
                IF DATABASE_PRINCIPAL_ID(N'docai_modelteam') IS NULL CREATE ROLE docai_modelteam AUTHORIZATION dbo;
                """);
        }
    }
}
