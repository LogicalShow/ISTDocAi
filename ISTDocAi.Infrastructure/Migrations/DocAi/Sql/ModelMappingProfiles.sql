-- Frozen stage 2 mapping schema. No runtime configuration is read by this migration.
CREATE TABLE dbo.MappingProfiles (
 ProfileId bigint IDENTITY NOT NULL CONSTRAINT PK_MappingProfiles PRIMARY KEY,
 ProfileCode varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT UQ_MappingProfiles_Code UNIQUE,
 ProfileVersion int NOT NULL CONSTRAINT CK_MappingProfiles_Version CHECK(ProfileVersion>0),
 DocumentTypeCode varchar(20) NOT NULL CONSTRAINT CK_MappingProfiles_Type CHECK(DocumentTypeCode COLLATE Latin1_General_100_BIN2 IN ('BL','MANIFEST')),
 MappingShape varchar(20) NOT NULL CONSTRAINT CK_MappingProfiles_Shape CHECK(MappingShape COLLATE Latin1_General_100_BIN2 IN ('LegacyFallback','ModelProfile')),
 ContainersField varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
 ContainerNumberField varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
 SealNumberField varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
 BillsOfLadingField varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
 SealedAtUtc datetime2(3) NULL,
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MappingProfiles_Created DEFAULT SYSUTCDATETIME(),
 CreatedBy nvarchar(128) NOT NULL CONSTRAINT DF_MappingProfiles_By DEFAULT ORIGINAL_LOGIN(),
 ChangeReference nvarchar(256) NOT NULL,
 CONSTRAINT CK_MappingProfiles_Fallback CHECK(MappingShape<>'LegacyFallback' OR DocumentTypeCode='BL'),
 CONSTRAINT CK_MappingProfiles_Code CHECK(DATALENGTH(ProfileCode)>0 AND ProfileCode NOT LIKE '%[^A-Za-z0-9_.-]%'),
 CONSTRAINT CK_MappingProfiles_Reference CHECK(LEN(ChangeReference)>0),
 CONSTRAINT CK_MappingProfiles_ContainersField CHECK(DATALENGTH(ContainersField) BETWEEN 1 AND 128 AND ContainersField NOT LIKE '%[^A-Za-z0-9_.-]%'),
 CONSTRAINT CK_MappingProfiles_ContainerNumberField CHECK(DATALENGTH(ContainerNumberField) BETWEEN 1 AND 128 AND ContainerNumberField NOT LIKE '%[^A-Za-z0-9_.-]%'),
 CONSTRAINT CK_MappingProfiles_SealNumberField CHECK(DATALENGTH(SealNumberField) BETWEEN 1 AND 128 AND SealNumberField NOT LIKE '%[^A-Za-z0-9_.-]%'),
 CONSTRAINT CK_MappingProfiles_BillsOfLadingField CHECK(DATALENGTH(BillsOfLadingField) BETWEEN 1 AND 128 AND BillsOfLadingField NOT LIKE '%[^A-Za-z0-9_.-]%')
);
CREATE TABLE dbo.MappingFields (
 ProfileId bigint NOT NULL CONSTRAINT FK_MappingFields_Profile REFERENCES dbo.MappingProfiles(ProfileId),
 MapKind varchar(12) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT CK_MappingFields_Kind CHECK(MapKind IN ('Field','Header','Bill','Container')),
 CanonicalKey varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
 AzureName varchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
 CONSTRAINT PK_MappingFields PRIMARY KEY(ProfileId,MapKind,CanonicalKey),
 CONSTRAINT CK_MappingFields_Key CHECK(DATALENGTH(CanonicalKey) BETWEEN 1 AND 64 AND CanonicalKey LIKE '[A-Za-z]%' AND CanonicalKey NOT LIKE '%[^A-Za-z0-9_]%'),
 CONSTRAINT CK_MappingFields_Name CHECK(DATALENGTH(AzureName) BETWEEN 1 AND 128 AND AzureName NOT LIKE '%[^A-Za-z0-9_.-]%')
);
CREATE TABLE dbo.ModelMappingBindings (
 ModelId varchar(64) NOT NULL CONSTRAINT PK_ModelMappingBindings PRIMARY KEY CONSTRAINT FK_ModelMappingBindings_Model REFERENCES dbo.ModelRegistry(ModelId),
 ProfileId bigint NOT NULL CONSTRAINT FK_ModelMappingBindings_Profile REFERENCES dbo.MappingProfiles(ProfileId)
);
CREATE INDEX IX_ModelMappingBindings_Profile ON dbo.ModelMappingBindings(ProfileId);
GO
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'bl-maersk-v1',1,N'BL',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'BillOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'DateOfIssue' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperAddress',N'ShipperAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeAddress',N'ConsigneeAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'hsCode',N'HSCode' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'totalGrossWeightKg',N'TotalGrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'Type' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeightKg',N'GrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'measurementCbm',N'MeasurementCbm' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-maersk-v1';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'bl-v2',1,N'BL',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'BillOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'DateOfIssue' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperAddress',N'ShipperAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeAddress',N'ConsigneeAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'hsCode',N'HSCode' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'totalGrossWeightKg',N'TotalGrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'Type' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeightKg',N'GrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'measurementCbm',N'MeasurementCbm' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v2';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'bl-v3',1,N'BL',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'BillOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'DateOfIssue' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperAddress',N'ShipperAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeAddress',N'ConsigneeAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'hsCode',N'HSCode' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'totalGrossWeightKg',N'TotalGrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'Type' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeightKg',N'GrossWeightKg' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'measurementCbm',N'MeasurementCbm' FROM dbo.MappingProfiles WHERE ProfileCode=N'bl-v3';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'manifest-v1',1,N'MANIFEST',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'billOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'shipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'consigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'vesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'voyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'portOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'portOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'issueDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'ContainerType' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'carrierName',N'CarrierName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'manifestDate',N'ManifestDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'shipperName',N'Shipper' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'consigneeName',N'Consignee' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v1';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'manifest-v2',1,N'MANIFEST',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'billOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'shipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'consigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'vesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'voyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'portOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'portOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'issueDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'ContainerType' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'carrierName',N'CarrierName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'manifestDate',N'ManifestDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'placeOfDelivery',N'PlaceOfDelivery' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'shipperAddress',N'ShipperAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'consigneeAddress',N'ConsigneeAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'containerCount',N'ContainerCount' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'hsCode',N'HSCode' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'packageType',N'PackageType' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'issueDate',N'IssueDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v2';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'manifest-v3',1,N'MANIFEST',N'ModelProfile',N'Containers',N'containerNumber',N'sealNumber',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'billOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'shipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'consigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'vesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'voyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'portOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'portOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'issueDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerNumber',N'ContainerNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'sealNumber',N'SealNo' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'containerType',N'ContainerType' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Container',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'carrierName',N'CarrierName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'manifestDate',N'ManifestDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Header',N'placeOfDelivery',N'PlaceOfDelivery' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'billOfLadingNumber',N'BLNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'shipperAddress',N'ShipperAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'consigneeAddress',N'ConsigneeAddress' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'notifyParty',N'NotifyParty' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'bookingNumber',N'BookingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'freightTerms',N'FreightTerms' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'packages',N'Packages' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'grossWeight',N'GrossWeight' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'description',N'Description' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'containerCount',N'ContainerCount' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'hsCode',N'HSCode' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'packageType',N'PackageType' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingFields SELECT ProfileId,N'Bill',N'issueDate',N'IssueDate' FROM dbo.MappingProfiles WHERE ProfileCode=N'manifest-v3';
INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference) VALUES (N'legacy',1,N'BL',N'LegacyFallback',N'Containers',N'ContainerNo',N'SealNo',N'BillsOfLading',N'Stage 2 frozen mapping seed');
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'billOfLadingNumber',N'BillOfLadingNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'shipperName',N'ShipperName' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'consigneeName',N'ConsigneeName' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'vesselName',N'VesselName' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'voyageNumber',N'VoyageNumber' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfLoading',N'PortOfLoading' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'portOfDischarge',N'PortOfDischarge' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';
INSERT dbo.MappingFields SELECT ProfileId,N'Field',N'issueDate',N'DateOfIssue' FROM dbo.MappingProfiles WHERE ProfileCode=N'legacy';

INSERT dbo.ModelMappingBindings(ModelId,ProfileId)
 SELECT m.ModelId,p.ProfileId FROM dbo.ModelRegistry m JOIN dbo.MappingProfiles p ON p.ProfileCode=m.ModelId COLLATE Latin1_General_100_BIN2
 WHERE m.Kind='extract' AND m.DocumentTypeCode=p.DocumentTypeCode;
-- Materialize one complete LegacyFallback-shaped copy for every existing implicit BL consumer.
DECLARE @model varchar(64), @id bigint;
DECLARE fallback_models CURSOR LOCAL FAST_FORWARD FOR
 SELECT ModelId FROM dbo.ModelRegistry m WHERE Kind='extract' AND DocumentTypeCode='BL'
 AND NOT EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b WHERE b.ModelId=m.ModelId);
OPEN fallback_models;
FETCH NEXT FROM fallback_models INTO @model;
WHILE @@FETCH_STATUS=0
BEGIN
 INSERT dbo.MappingProfiles(ProfileCode,ProfileVersion,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,ChangeReference)
 SELECT @model,1,DocumentTypeCode,MappingShape,ContainersField,ContainerNumberField,SealNumberField,BillsOfLadingField,N'Stage 2 existing BL fallback copy'
 FROM dbo.MappingProfiles WHERE ProfileCode='legacy';
 SET @id=SCOPE_IDENTITY();
 INSERT dbo.MappingFields SELECT @id,MapKind,CanonicalKey,AzureName FROM dbo.MappingFields WHERE ProfileId=(SELECT ProfileId FROM dbo.MappingProfiles WHERE ProfileCode='legacy');
 INSERT dbo.ModelMappingBindings VALUES(@model,@id);
 FETCH NEXT FROM fallback_models INTO @model;
END;
CLOSE fallback_models;
DEALLOCATE fallback_models;
UPDATE p SET SealedAtUtc=SYSUTCDATETIME() FROM dbo.MappingProfiles p
 WHERE EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b JOIN dbo.ModelRegistry m ON m.ModelId=b.ModelId
 WHERE b.ProfileId=p.ProfileId AND (m.ActivatedAtUtc IS NOT NULL OR m.Status='Active'
 OR EXISTS(SELECT 1 FROM dbo.AzureOperations o WHERE o.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.Jobs j WHERE j.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.ModelId=m.ModelId)));
GO
-- Only mapping writes lock the parent profile; normal processing has no mapping triggers.
-- Sealing is derived lazily here. HOLDLOCK also serializes the 256-row cap.
CREATE TRIGGER dbo.TR_MappingProfiles_Immutable ON dbo.MappingProfiles AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @locked bigint;
 SELECT @locked=p.ProfileId FROM dbo.MappingProfiles p WITH(UPDLOCK,HOLDLOCK)
 WHERE p.ProfileId IN(SELECT ProfileId FROM inserted UNION SELECT ProfileId FROM deleted);
 IF EXISTS(SELECT 1 FROM deleted p WHERE p.SealedAtUtc IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b JOIN dbo.ModelRegistry m ON m.ModelId=b.ModelId
 WHERE b.ProfileId=p.ProfileId AND (m.ActivatedAtUtc IS NOT NULL OR m.Status='Active'
 OR EXISTS(SELECT 1 FROM dbo.AzureOperations o WHERE o.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.Jobs j WHERE j.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.ModelId=m.ModelId))))
  THROW 51200,'Mapping profile is sealed.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.MappingFields f ON f.ProfileId=i.ProfileId
 WHERE i.MappingShape='LegacyFallback' AND f.MapKind IN ('Header','Bill'))
  THROW 51202,'Map kind does not match mapping shape.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ModelMappingBindings b ON b.ProfileId=i.ProfileId
 JOIN dbo.ModelRegistry m ON m.ModelId=b.ModelId WHERE i.DocumentTypeCode<>m.DocumentTypeCode)
  THROW 51204,'Mapping binding does not match extract model.',1;
END;
GO
CREATE TRIGGER dbo.TR_MappingFields_Immutable ON dbo.MappingFields AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @locked bigint;
 SELECT @locked=p.ProfileId FROM dbo.MappingProfiles p WITH(UPDLOCK,HOLDLOCK)
 WHERE p.ProfileId IN(SELECT ProfileId FROM inserted UNION SELECT ProfileId FROM deleted);
 IF EXISTS(SELECT 1 FROM dbo.MappingProfiles p WHERE (p.SealedAtUtc IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b JOIN dbo.ModelRegistry m ON m.ModelId=b.ModelId
 WHERE b.ProfileId=p.ProfileId AND (m.ActivatedAtUtc IS NOT NULL OR m.Status='Active'
 OR EXISTS(SELECT 1 FROM dbo.AzureOperations o WHERE o.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.Jobs j WHERE j.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.ModelId=m.ModelId))))
 AND p.ProfileId IN(SELECT ProfileId FROM inserted UNION SELECT ProfileId FROM deleted))
  THROW 51200,'Mapping profile is sealed.',1;
 IF EXISTS(SELECT 1 FROM dbo.MappingFields f WITH(UPDLOCK,HOLDLOCK)
 WHERE f.ProfileId IN(SELECT ProfileId FROM inserted) GROUP BY f.ProfileId HAVING COUNT_BIG(*)>256)
  THROW 51201,'Mapping profile row limit exceeded.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.MappingProfiles p ON p.ProfileId=i.ProfileId
 WHERE p.MappingShape='LegacyFallback' AND i.MapKind IN ('Header','Bill'))
  THROW 51202,'Map kind does not match mapping shape.',1;
END;
GO
CREATE TRIGGER dbo.TR_ModelMappingBindings_Immutable ON dbo.ModelMappingBindings AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @locked bigint;
 SELECT @locked=p.ProfileId FROM dbo.MappingProfiles p WITH(UPDLOCK,HOLDLOCK)
 WHERE p.ProfileId IN(SELECT ProfileId FROM inserted UNION SELECT ProfileId FROM deleted);
 -- Permanent means even draft bindings cannot be reassigned. Draft contents remain editable.
 IF EXISTS(SELECT 1 FROM deleted) THROW 51203,'Model mapping binding is permanent.',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.ModelRegistry m ON m.ModelId=i.ModelId
 JOIN dbo.MappingProfiles p ON p.ProfileId=i.ProfileId
 WHERE m.Kind<>'extract' OR m.DocumentTypeCode<>p.DocumentTypeCode)
  THROW 51204,'Mapping binding does not match extract model.',1;
 IF EXISTS(SELECT 1 FROM dbo.MappingProfiles p WHERE (p.SealedAtUtc IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.ModelMappingBindings b JOIN dbo.ModelRegistry m ON m.ModelId=b.ModelId
 WHERE b.ProfileId=p.ProfileId AND (m.ActivatedAtUtc IS NOT NULL OR m.Status='Active'
 OR EXISTS(SELECT 1 FROM dbo.AzureOperations o WHERE o.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.Jobs j WHERE j.ModelId=m.ModelId)
 OR EXISTS(SELECT 1 FROM dbo.UsageLedger l WHERE l.ModelId=m.ModelId))))
 AND p.ProfileId IN(SELECT ProfileId FROM inserted))
  THROW 51200,'Mapping profile is sealed.',1;
END;
GO
GRANT SELECT ON dbo.MappingProfiles TO docai_runtime;
DENY INSERT,UPDATE,DELETE ON dbo.MappingProfiles TO docai_runtime;
GRANT SELECT,INSERT ON dbo.MappingProfiles TO docai_modelteam;
GRANT SELECT ON dbo.MappingFields TO docai_runtime;
DENY INSERT,UPDATE,DELETE ON dbo.MappingFields TO docai_runtime;
GRANT SELECT,INSERT ON dbo.MappingFields TO docai_modelteam;
GRANT SELECT ON dbo.ModelMappingBindings TO docai_runtime;
DENY INSERT,UPDATE,DELETE ON dbo.ModelMappingBindings TO docai_runtime;
GRANT SELECT,INSERT ON dbo.ModelMappingBindings TO docai_modelteam;
GRANT UPDATE,DELETE ON dbo.MappingProfiles TO docai_modelteam;
GRANT UPDATE,DELETE ON dbo.MappingFields TO docai_modelteam;
DENY UPDATE,DELETE ON dbo.ModelMappingBindings TO docai_modelteam;
-- SealedAtUtc is database-owned; a model-team draft cannot forge or clear sealing.
DENY UPDATE(SealedAtUtc) ON dbo.MappingProfiles TO docai_modelteam;
