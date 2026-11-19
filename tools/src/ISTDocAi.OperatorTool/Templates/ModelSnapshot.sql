-- Group range locks, followed by binding/profile/field/route locks, survive until caller commits.
DECLARE @model varchar(64),@profile bigint,@shape varchar(20),@mappingFingerprint varchar(64),@effective nvarchar(max);
SELECT @model=ModelId FROM dbo.ModelRegistry WITH(UPDLOCK,HOLDLOCK) WHERE $group AND Status='Active';
SELECT @profile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId=@model;
DECLARE @p TABLE(ProfileId bigint,ProfileCode varchar(64),ProfileVersion int,DocumentTypeCode varchar(20),MappingShape varchar(20),ContainersField varchar(128),ContainerNumberField varchar(128),SealNumberField varchar(128),BillsOfLadingField varchar(128),ChangeReference nvarchar(256));
INSERT @p SELECT ProfileId,ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileId=@profile;
DECLARE @f TABLE(MapKind varchar(12),CanonicalKey varchar(64),AzureName varchar(128));
INSERT @f SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WITH(UPDLOCK,HOLDLOCK) WHERE ProfileId=@profile;
DECLARE @c TABLE(ClassName varchar(64),TargetDocumentTypeCode varchar(20),ShippingLineCode varchar(20));
INSERT @c SELECT ClassName,TargetDocumentTypeCode,ShippingLineCode FROM dbo.ClassifierClasses WITH(UPDLOCK,HOLDLOCK) WHERE ClassifierModelId=@model;
-- Algorithm 2: same sorted property names and effective maps as MappingFingerprint.ComputeEffective.
-- Mapping keys/selectors are validated ASCII identifiers, so JSON escaping is unnecessary here.
DECLARE @maps TABLE(Kind varchar(12),Json nvarchar(max));
INSERT @maps SELECT k.Kind,N'{'+COALESCE((SELECT STRING_AGG(CONVERT(nvarchar(max),N'"'+CanonicalKey+N'":"'+AzureName+N'"'),N',') WITHIN GROUP(ORDER BY CanonicalKey COLLATE Latin1_General_100_BIN2) FROM @f WHERE MapKind=k.Kind),N'')+N'}'
FROM (VALUES('Field'),('Container'),('Header'),('Bill')) k(Kind);
SELECT @shape=MappingShape,@effective=N'{"algorithmVersion":"2",'+
 CASE WHEN MappingShape='ModelProfile' THEN N'"billFieldMap":'+(SELECT Json FROM @maps WHERE Kind='Bill')+N',"billsOfLadingField":"'+BillsOfLadingField+N'",' ELSE N'' END+
 N'"containerFieldMap":'+(SELECT Json FROM @maps WHERE Kind='Container')+N',"containerNumberField":"'+ContainerNumberField+N'","containersField":"'+ContainersField+N'","contractVersion":"1.0","documentType":"'+DocumentTypeCode+N'","fieldMap":'+(SELECT Json FROM @maps WHERE Kind='Field')+N','+
 CASE WHEN MappingShape='ModelProfile' THEN N'"headerFieldMap":'+(SELECT Json FROM @maps WHERE Kind='Header')+N',' ELSE N'' END+
 N'"preserveExtractedText":'+CASE WHEN MappingShape='ModelProfile' THEN N'true' ELSE N'false' END+N',"sealNumberField":"'+SealNumberField+N'"}' FROM @p;
-- UTF-8 matches Utf8JsonWriter in the runtime fingerprint implementation.
SET @mappingFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONVERT(varchar(max),@effective COLLATE Latin1_General_100_BIN2_UTF8))),2));
DECLARE @snapshot nvarchar(max)=(SELECT
 '$documentType' DocumentTypeCode,NULLIF('$shippingLine','') ShippingLineCode,
 JSON_QUERY((SELECT ModelId,DocumentTypeCode,ShippingLineCode,Kind,MinFieldConfidence,TestFieldAccuracy,Version,ActivatedAtUtc FROM dbo.ModelRegistry WITH(UPDLOCK,HOLDLOCK) WHERE ModelId=@model FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) Model,
 JSON_QUERY((SELECT p.*,JSON_QUERY((SELECT MapKind,CanonicalKey,AzureName FROM @f ORDER BY MapKind COLLATE Latin1_General_100_BIN2,CanonicalKey COLLATE Latin1_General_100_BIN2 FOR JSON PATH,INCLUDE_NULL_VALUES)) MappingFields FROM @p p FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) MappingProfile,
 @mappingFingerprint MappingFingerprint,
 JSON_QUERY((SELECT ClassName,TargetDocumentTypeCode,ShippingLineCode FROM @c ORDER BY ClassName COLLATE Latin1_General_100_BIN2 FOR JSON PATH,INCLUDE_NULL_VALUES)) ClassifierClasses
 FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
DECLARE @snapshotToken varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),@snapshot)),2);
