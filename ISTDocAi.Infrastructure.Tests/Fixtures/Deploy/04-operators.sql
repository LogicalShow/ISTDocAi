-- Run AFTER DocAi has created and migrated the database.
USE [master];
GO
IF SUSER_ID(N'FAKE\DocAiOperators') IS NULL CREATE LOGIN [FAKE\DocAiOperators] FROM WINDOWS;
GO
USE [DocAi];
GO
IF DATABASE_PRINCIPAL_ID(N'FAKE\DocAiOperators') IS NULL CREATE USER [FAKE\DocAiOperators] FOR LOGIN [FAKE\DocAiOperators];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N'docai_operator') AND member_principal_id=DATABASE_PRINCIPAL_ID(N'FAKE\DocAiOperators')) ALTER ROLE [docai_operator] ADD MEMBER [FAKE\DocAiOperators];
GO
USE [master];
GO
IF SUSER_ID(N'FAKE\DocAiModels') IS NULL CREATE LOGIN [FAKE\DocAiModels] FROM WINDOWS;
GO
USE [DocAi];
GO
IF DATABASE_PRINCIPAL_ID(N'FAKE\DocAiModels') IS NULL CREATE USER [FAKE\DocAiModels] FOR LOGIN [FAKE\DocAiModels];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N'docai_modelteam') AND member_principal_id=DATABASE_PRINCIPAL_ID(N'FAKE\DocAiModels')) ALTER ROLE [docai_modelteam] ADD MEMBER [FAKE\DocAiModels];
GO

