SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- New models enter as Testing: a mapping profile cannot be bound to a model that is already Active.
INSERT dbo.ModelRegistry(ModelId,DocumentTypeCode,Kind,Version,Status,TestFieldAccuracy,MinFieldConfidence)
SELECT v.ModelId,v.Doc,v.Kind,v.Version,'Testing',v.Accuracy,v.Confidence
FROM (VALUES ('bl-v3','BL','extract',3,0.97,0.8),('manifest-v3','MANIFEST','extract',3,0.92,0.8),('IST-Classifier-v6','AUTO','classify',6,0.91,0.5)) v(ModelId,Doc,Kind,Version,Accuracy,Confidence)
WHERE NOT EXISTS(SELECT 1 FROM dbo.ModelRegistry m WHERE m.ModelId=v.ModelId);
INSERT dbo.ModelMappingBindings(ModelId,ProfileId)
SELECT m.ModelId,p.ProfileId FROM dbo.ModelRegistry m JOIN dbo.MappingProfiles p ON p.ProfileCode=m.ModelId COLLATE Latin1_General_100_BIN2
WHERE m.ModelId IN ('bl-v3','manifest-v3') AND NOT EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b WHERE b.ModelId=m.ModelId);
INSERT dbo.ClassifierClasses(ClassifierModelId,ClassName,TargetDocumentTypeCode,ShippingLineCode)
SELECT 'IST-Classifier-v6',v.Name,v.Doc,NULL FROM (VALUES('bill-of-lading','BL'),('cargo-manifest','MANIFEST'),('other',NULL)) v(Name,Doc)
WHERE NOT EXISTS(SELECT 1 FROM dbo.ClassifierClasses c WHERE c.ClassifierModelId='IST-Classifier-v6' AND c.ClassName=v.Name);
UPDATE dbo.ModelRegistry SET Status='Active',ActivatedAtUtc=SYSUTCDATETIME()
WHERE ModelId IN ('bl-v3','manifest-v3','IST-Classifier-v6') AND Status='Testing';
COMMIT;
