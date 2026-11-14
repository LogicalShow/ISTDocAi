-- DocAi creates the database on first start and becomes its owner.
-- No database user or role is needed for the application identity.
USE [master];
GO
IF SUSER_ID(N'FAKE\DocAiApp') IS NULL CREATE LOGIN [FAKE\DocAiApp] FROM WINDOWS;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_role_members WHERE role_principal_id=SUSER_ID(N'dbcreator') AND member_principal_id=SUSER_ID(N'FAKE\DocAiApp')) ALTER SERVER ROLE [dbcreator] ADD MEMBER [FAKE\DocAiApp];
GO

