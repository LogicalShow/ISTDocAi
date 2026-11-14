-- Target: DocAi database (select the intended database in SSMS or sqlcmd -x -d).
-- Run by: SQL administrator. Required deployment role: docai_modelteam (db_owner also suffices).
-- Run AFTER database.migration_completed (or /health/ready).
-- Grants are installed by EF migrations; legacy/test reference db/migrations/02_docai_grants.sql: SELECT on dbo schema;
-- INSERT, UPDATE on ModelRegistry; INSERT, UPDATE, DELETE on ClassifierClasses.
-- Safe to rerun: already Active models are unchanged. Stop on any SQL error.
-- Each registration/activation batch has its own transaction; earlier batches may have committed.
-- Activates BL: bl-v3; accuracy=0.97; MANIFEST: manifest-v3; accuracy=0.92
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH('dbo.ClassifierClasses','TargetDocumentTypeCode') IS NULL
        THROW 50001, 'Apply the AUTO/MANIFEST upgrade first.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3'
               AND (DocumentTypeCode<>'MANIFEST' OR Kind<>'extract' OR ShippingLineCode IS NOT NULL))

        THROW 50001, 'An existing model ID belongs to a different group. No rows changed.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3')
        INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status,MinFieldConfidence,TestFieldAccuracy)
        VALUES('manifest-v3','MANIFEST',NULL,'extract',(SELECT ISNULL(MAX(CONVERT(int,Version)),0)+1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE DocumentTypeCode='MANIFEST' AND ShippingLineCode IS NULL AND Kind='extract'),'Testing',0.8,0.92);
-- Binding and profile locks live until the caller commits registration/activation.
DECLARE @mappingProfile bigint, @boundProfile bigint;
SELECT @boundProfile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3';
SELECT @mappingProfile=ProfileId FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileCode='manifest-v3';
IF @boundProfile IS NOT NULL SET @mappingProfile=@boundProfile;

IF @mappingProfile IS NULL THROW 51205, 'no mapping profile for manifest-v3; supply -MappingFile', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile AND DocumentTypeCode='MANIFEST')
    THROW 51204, 'Mapping binding document type mismatch.', 1;
IF @boundProfile IS NULL
    INSERT dbo.ModelMappingBindings(ModelId,ProfileId) VALUES('manifest-v3',@mappingProfile);

    COMMIT;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.ModelRegistry WHERE ModelId='manifest-v3' AND Status='Active') THEN 'Router models already Active; no changes.' ELSE 'Router models/classes registered without activation.' END;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='bl-v3'
               AND (DocumentTypeCode<>'BL' OR Kind<>'extract' OR ShippingLineCode IS NOT NULL))
        THROW 50001, 'Model group mismatch.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WHERE ModelId='bl-v3' AND Status='Active')
    BEGIN
EXEC sys.sp_executesql N'-- Binding and profile locks live until the caller commits registration/activation.
DECLARE @mappingProfile bigint, @boundProfile bigint;
SELECT @boundProfile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId=''bl-v3'';
SELECT @mappingProfile=ProfileId FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileCode=''bl-v3'';
IF @boundProfile IS NOT NULL SET @mappingProfile=@boundProfile;

IF @mappingProfile IS NULL THROW 51205, ''no mapping profile for bl-v3; supply -MappingFile'', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile AND DocumentTypeCode=''BL'')
    THROW 51204, ''Mapping binding document type mismatch.'', 1;
IF @boundProfile IS NULL
    INSERT dbo.ModelMappingBindings(ModelId,ProfileId) VALUES(''bl-v3'',@mappingProfile);';
    END;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='bl-v3' AND Status='Active')
        SELECT 'bl-v3 already Active; no changes.';
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelRegistry WHERE ModelId='bl-v3')
        BEGIN
            DECLARE @version int;
            SELECT @version=ISNULL(MAX(CONVERT(int,Version)),0)+1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK)
            WHERE DocumentTypeCode='BL' AND ShippingLineCode IS NULL AND Kind='extract';
            INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status)
            VALUES('bl-v3','BL',NULL,'extract',@version,'Testing');
        END;
EXEC sys.sp_executesql N'-- Binding and profile locks live until the caller commits registration/activation.
DECLARE @mappingProfile bigint, @boundProfile bigint;
SELECT @boundProfile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId=''bl-v3'';
SELECT @mappingProfile=ProfileId FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileCode=''bl-v3'';
IF @boundProfile IS NOT NULL SET @mappingProfile=@boundProfile;

IF @mappingProfile IS NULL THROW 51205, ''no mapping profile for bl-v3; supply -MappingFile'', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile AND DocumentTypeCode=''BL'')
    THROW 51204, ''Mapping binding document type mismatch.'', 1;
IF @boundProfile IS NULL
    INSERT dbo.ModelMappingBindings(ModelId,ProfileId) VALUES(''bl-v3'',@mappingProfile);';
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelRegistry WHERE ModelId='bl-v3' AND Status IN ('Draft','Testing','Retired'))
            THROW 50001, 'Only Draft, Testing or Retired models can be activated.', 1;
        -- Retired models in this same group can be re-tested and activated for rollback.
        UPDATE dbo.ModelRegistry SET Status='Testing',TestFieldAccuracy=0.97,MinFieldConfidence=0.8
        WHERE ModelId='bl-v3';
        EXEC sys.sp_executesql N'SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @doc varchar(20), @line varchar(20), @kind varchar(10);
SELECT @doc = DocumentTypeCode, @line = ShippingLineCode, @kind = Kind
FROM dbo.ModelRegistry WITH (UPDLOCK, HOLDLOCK)
WHERE ModelId = @newModelId AND Status = ''Testing'' AND TestFieldAccuracy >= @minimumAccuracy;
IF @doc IS NULL THROW 50001, ''The new model is not in Testing or is below the accuracy gate.'', 1;
IF @kind = ''extract'' AND NOT EXISTS (
    SELECT 1 FROM dbo.ModelMappingBindings b WITH (UPDLOCK,HOLDLOCK)
    JOIN dbo.MappingProfiles p WITH (UPDLOCK,HOLDLOCK) ON p.ProfileId=b.ProfileId
    WHERE b.ModelId=@newModelId AND p.DocumentTypeCode=@doc)
    THROW 51205, ''Extract activation requires a matching mapping binding; register with -MappingFile.'', 1;
IF @kind = ''classify''
BEGIN
    -- HOLDLOCK protects the class map and target groups until activation commits.
    IF NOT EXISTS (SELECT 1 FROM dbo.ClassifierClasses WITH (UPDLOCK, HOLDLOCK)
                   WHERE ClassifierModelId = @newModelId AND TargetDocumentTypeCode IS NOT NULL)
        THROW 50003, ''A classifier needs at least one routable class before activation.'', 1;
    IF EXISTS (SELECT 1 FROM dbo.ClassifierClasses AS c WITH (UPDLOCK, HOLDLOCK)
               WHERE c.ClassifierModelId = @newModelId
                 AND ((c.TargetDocumentTypeCode IS NULL AND c.ShippingLineCode IS NOT NULL)
                   OR c.TargetDocumentTypeCode = ''AUTO''
                   OR (@doc <> ''AUTO'' AND (c.TargetDocumentTypeCode IS NULL OR c.TargetDocumentTypeCode <> @doc))
                   OR (c.TargetDocumentTypeCode IS NOT NULL AND NOT EXISTS
                       (SELECT 1 FROM dbo.ModelRegistry AS t WITH (UPDLOCK, HOLDLOCK)
                        WHERE t.DocumentTypeCode = c.TargetDocumentTypeCode AND t.Kind = ''extract''
                          AND (t.ShippingLineCode = c.ShippingLineCode OR (t.ShippingLineCode IS NULL AND c.ShippingLineCode IS NULL))))))
        THROW 50003, ''Classifier targets must identify supported extraction groups or intentional AUTO rejection.'', 1;
END;

UPDATE dbo.ModelRegistry WITH (HOLDLOCK)
SET Status = ''Retired'', RetiredAtUtc = SYSUTCDATETIME()
WHERE DocumentTypeCode = @doc AND Kind = @kind AND Status = ''Active''
  AND (ShippingLineCode = @line OR (ShippingLineCode IS NULL AND @line IS NULL));

UPDATE dbo.ModelRegistry SET Status = ''Active'', ActivatedAtUtc = SYSUTCDATETIME() WHERE ModelId = @newModelId;
COMMIT;

', N'@newModelId varchar(64), @minimumAccuracy decimal(5,4)',
             @newModelId='bl-v3', @minimumAccuracy=0.95;
        SELECT 'bl-v3 activated.';
    END;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3'
               AND (DocumentTypeCode<>'MANIFEST' OR Kind<>'extract' OR ShippingLineCode IS NOT NULL))
        THROW 50001, 'Model group mismatch.', 1;
-- Binding and profile locks live until the caller commits registration/activation.
DECLARE @mappingProfile bigint, @boundProfile bigint;
SELECT @boundProfile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3';
SELECT @mappingProfile=ProfileId FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileCode='manifest-v3';
IF @boundProfile IS NOT NULL SET @mappingProfile=@boundProfile;

IF @mappingProfile IS NULL THROW 51205, 'no mapping profile for manifest-v3; supply -MappingFile', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile AND DocumentTypeCode='MANIFEST')
    THROW 51204, 'Mapping binding document type mismatch.', 1;
IF @boundProfile IS NULL
    INSERT dbo.ModelMappingBindings(ModelId,ProfileId) VALUES('manifest-v3',@mappingProfile);
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3' AND Status='Active')
        SELECT 'manifest-v3 already Active; no changes.';
    ELSE
    BEGIN

        IF NOT EXISTS (SELECT 1 FROM dbo.ModelRegistry WHERE ModelId='manifest-v3' AND (Status IN ('Draft','Testing') OR (DocumentTypeCode='MANIFEST' AND Status='Retired')))
            THROW 50001, 'Register the model first; only Draft/Testing models or Retired MANIFEST models can be activated.', 1;
        UPDATE dbo.ModelRegistry SET Status='Testing',TestFieldAccuracy=0.92 WHERE ModelId='manifest-v3';
        EXEC sys.sp_executesql N'SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @doc varchar(20), @line varchar(20), @kind varchar(10);
SELECT @doc = DocumentTypeCode, @line = ShippingLineCode, @kind = Kind
FROM dbo.ModelRegistry WITH (UPDLOCK, HOLDLOCK)
WHERE ModelId = @newModelId AND Status = ''Testing'' AND TestFieldAccuracy >= @minimumAccuracy;
IF @doc IS NULL THROW 50001, ''The new model is not in Testing or is below the accuracy gate.'', 1;
IF @kind = ''extract'' AND NOT EXISTS (
    SELECT 1 FROM dbo.ModelMappingBindings b WITH (UPDLOCK,HOLDLOCK)
    JOIN dbo.MappingProfiles p WITH (UPDLOCK,HOLDLOCK) ON p.ProfileId=b.ProfileId
    WHERE b.ModelId=@newModelId AND p.DocumentTypeCode=@doc)
    THROW 51205, ''Extract activation requires a matching mapping binding; register with -MappingFile.'', 1;
IF @kind = ''classify''
BEGIN
    -- HOLDLOCK protects the class map and target groups until activation commits.
    IF NOT EXISTS (SELECT 1 FROM dbo.ClassifierClasses WITH (UPDLOCK, HOLDLOCK)
                   WHERE ClassifierModelId = @newModelId AND TargetDocumentTypeCode IS NOT NULL)
        THROW 50003, ''A classifier needs at least one routable class before activation.'', 1;
    IF EXISTS (SELECT 1 FROM dbo.ClassifierClasses AS c WITH (UPDLOCK, HOLDLOCK)
               WHERE c.ClassifierModelId = @newModelId
                 AND ((c.TargetDocumentTypeCode IS NULL AND c.ShippingLineCode IS NOT NULL)
                   OR c.TargetDocumentTypeCode = ''AUTO''
                   OR (@doc <> ''AUTO'' AND (c.TargetDocumentTypeCode IS NULL OR c.TargetDocumentTypeCode <> @doc))
                   OR (c.TargetDocumentTypeCode IS NOT NULL AND NOT EXISTS
                       (SELECT 1 FROM dbo.ModelRegistry AS t WITH (UPDLOCK, HOLDLOCK)
                        WHERE t.DocumentTypeCode = c.TargetDocumentTypeCode AND t.Kind = ''extract''
                          AND (t.ShippingLineCode = c.ShippingLineCode OR (t.ShippingLineCode IS NULL AND c.ShippingLineCode IS NULL))))))
        THROW 50003, ''Classifier targets must identify supported extraction groups or intentional AUTO rejection.'', 1;
END;

UPDATE dbo.ModelRegistry WITH (HOLDLOCK)
SET Status = ''Retired'', RetiredAtUtc = SYSUTCDATETIME()
WHERE DocumentTypeCode = @doc AND Kind = @kind AND Status = ''Active''
  AND (ShippingLineCode = @line OR (ShippingLineCode IS NULL AND @line IS NULL));

UPDATE dbo.ModelRegistry SET Status = ''Active'', ActivatedAtUtc = SYSUTCDATETIME() WHERE ModelId = @newModelId;
COMMIT;

', N'@newModelId varchar(64), @minimumAccuracy decimal(5,4)',
             @newModelId='manifest-v3', @minimumAccuracy=0.6;
        SELECT 'manifest-v3 activated.';
    END;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
SELECT ModelId, DocumentTypeCode, ShippingLineCode, Kind, Version, Status, MinFieldConfidence, TestFieldAccuracy FROM dbo.ModelRegistry WHERE ModelId IN ('bl-v3','manifest-v3') ORDER BY ModelId;
