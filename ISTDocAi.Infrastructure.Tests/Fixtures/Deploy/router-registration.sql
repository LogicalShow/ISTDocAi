SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH('dbo.ClassifierClasses','TargetDocumentTypeCode') IS NULL
        THROW 50001, 'Apply the AUTO/MANIFEST upgrade first.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='manifest-v3'
               AND (DocumentTypeCode<>'MANIFEST' OR Kind<>'extract' OR ShippingLineCode IS NOT NULL))
       OR EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='IST-Classifier-v6'
                  AND (DocumentTypeCode<>'AUTO' OR Kind<>'classify' OR ShippingLineCode IS NOT NULL))
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
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelRegistry WITH (UPDLOCK,HOLDLOCK) WHERE ModelId='IST-Classifier-v6')
        INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,ShippingLineCode,Kind,Version,Status,MinFieldConfidence,TestFieldAccuracy)
        VALUES('IST-Classifier-v6','AUTO',NULL,'classify',1,'Draft',0.8,0.91);
    DECLARE @classes TABLE(ClassName varchar(64), TargetDocumentTypeCode varchar(20) NULL, ShippingLineCode varchar(20) NULL);
    INSERT @classes VALUES('bill-of-lading','BL',NULL),('cargo-manifest','MANIFEST',NULL),('other',NULL,NULL);
    IF EXISTS (SELECT 1 FROM @classes AS desired JOIN dbo.ClassifierClasses AS existing WITH (UPDLOCK,HOLDLOCK)
               ON existing.ClassifierModelId='IST-Classifier-v6' AND existing.ClassName=desired.ClassName
               WHERE ISNULL(existing.TargetDocumentTypeCode,'')<>ISNULL(desired.TargetDocumentTypeCode,'')
                  OR ISNULL(existing.ShippingLineCode,'')<>ISNULL(desired.ShippingLineCode,''))
        THROW 50001, 'Existing class targets differ. No rows changed.', 1;
    INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode)
    SELECT 'IST-Classifier-v6',desired.ClassName,desired.TargetDocumentTypeCode,desired.ShippingLineCode FROM @classes AS desired
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ClassifierClasses AS existing WITH (UPDLOCK,HOLDLOCK)
                      WHERE existing.ClassifierModelId='IST-Classifier-v6' AND existing.ClassName=desired.ClassName);
    COMMIT;
    SELECT 'Router models/classes registered without activation.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;