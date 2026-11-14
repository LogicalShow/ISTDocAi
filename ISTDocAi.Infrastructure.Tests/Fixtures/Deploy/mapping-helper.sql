-- Binding and profile locks live until the caller commits registration/activation.
DECLARE @mappingProfile bigint, @boundProfile bigint;
SELECT @boundProfile=ProfileId FROM dbo.ModelMappingBindings WITH(UPDLOCK,HOLDLOCK) WHERE ModelId='baseline-manifest';
SELECT @mappingProfile=ProfileId FROM dbo.MappingProfiles WITH(UPDLOCK,HOLDLOCK) WHERE ProfileCode='baseline-manifest';
IF @boundProfile IS NOT NULL SET @mappingProfile=@boundProfile;
DECLARE @mappingRows TABLE(MapKind varchar(12) COLLATE Latin1_General_100_BIN2, CanonicalKey varchar(64) COLLATE Latin1_General_100_BIN2, AzureName varchar(128) COLLATE Latin1_General_100_BIN2);
INSERT @mappingRows VALUES ('Field','billOfLadingNumber','billOfLadingNumber'),('Field','shipperName','shipperName'),('Field','consigneeName','consigneeName'),('Field','vesselName','vesselName'),('Field','voyageNumber','voyageNumber'),('Field','portOfLoading','portOfLoading'),('Field','portOfDischarge','portOfDischarge'),('Field','issueDate','issueDate'),('Container','billOfLadingNumber','BLNumber'),('Container','containerNumber','ContainerNo'),('Container','sealNumber','SealNo'),('Container','containerType','ContainerType'),('Container','packages','Packages'),('Container','grossWeight','GrossWeight'),('Container','description','Description'),('Header','carrierName','CarrierName'),('Header','vesselName','VesselName'),('Header','voyageNumber','VoyageNumber'),('Header','portOfLoading','PortOfLoading'),('Header','portOfDischarge','PortOfDischarge'),('Header','manifestDate','ManifestDate'),('Header','placeOfDelivery','PlaceOfDelivery'),('Bill','billOfLadingNumber','BLNumber'),('Bill','shipperName','ShipperName'),('Bill','shipperAddress','ShipperAddress'),('Bill','consigneeName','ConsigneeName'),('Bill','consigneeAddress','ConsigneeAddress'),('Bill','notifyParty','NotifyParty'),('Bill','bookingNumber','BookingNumber'),('Bill','freightTerms','FreightTerms'),('Bill','packages','Packages'),('Bill','grossWeight','GrossWeight'),('Bill','description','Description'),('Bill','containerCount','ContainerCount'),('Bill','hsCode','HSCode'),('Bill','packageType','PackageType'),('Bill','issueDate','IssueDate');
IF @mappingProfile IS NULL
BEGIN
    INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference)
    VALUES('baseline-manifest',1,'MANIFEST','ModelProfile','Containers','containerNumber','sealNumber','BillsOfLading',CONVERT(nvarchar(256),0x5200450050004C004100430045002D0077006900740068002D00720065007600690065007700650064002D006300680061006E00670065002D007200650066006500720065006E0063006500));
    SET @mappingProfile=SCOPE_IDENTITY();
    INSERT dbo.MappingFields(ProfileId,MapKind,CanonicalKey,AzureName) SELECT @mappingProfile,MapKind,CanonicalKey,AzureName FROM @mappingRows;
END
ELSE IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile
    AND MappingShape='ModelProfile' AND ContainersField='Containers'
    AND ContainerNumberField='containerNumber' AND SealNumberField='sealNumber'
    AND BillsOfLadingField='BillsOfLading'
    AND ChangeReference COLLATE Latin1_General_100_BIN2=CONVERT(nvarchar(256),0x5200450050004C004100430045002D0077006900740068002D00720065007600690065007700650064002D006300680061006E00670065002D007200650066006500720065006E0063006500) COLLATE Latin1_General_100_BIN2 AND DATALENGTH(ChangeReference)=DATALENGTH(CONVERT(nvarchar(256),0x5200450050004C004100430045002D0077006900740068002D00720065007600690065007700650064002D006300680061006E00670065002D007200650066006500720065006E0063006500)))
    OR EXISTS (SELECT MapKind,CanonicalKey,AzureName FROM @mappingRows EXCEPT SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=@mappingProfile)
    OR EXISTS (SELECT MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=@mappingProfile EXCEPT SELECT MapKind,CanonicalKey,AzureName FROM @mappingRows)
    THROW 51205, 'Conflicting mapping profile content; existing profiles are never overwritten.', 1;
IF @mappingProfile IS NULL THROW 51205, 'no mapping profile for baseline-manifest; supply -MappingFile', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.MappingProfiles WHERE ProfileId=@mappingProfile AND DocumentTypeCode='MANIFEST')
    THROW 51204, 'Mapping binding document type mismatch.', 1;
IF @boundProfile IS NULL
    INSERT dbo.ModelMappingBindings(ModelId,ProfileId) VALUES('baseline-manifest',@mappingProfile);