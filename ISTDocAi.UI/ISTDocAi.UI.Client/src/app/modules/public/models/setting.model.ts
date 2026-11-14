// Setting Response - matches SettingResponse from backend
export interface SettingResponse {
  Id: string;
  RowVersion: string; // Base64 encoded byte array
  CompanyNameEn: string;
  CompanyNameAr?: string;
  CountryId?: string;
  CountryLongName?: string;
  CountryShortName?: string;
  CountryNationalityMaleName?: string;
  CountryNationalityFemaleName?: string;
  CompanyAddress: string;
  CompanyPhoneOne: string;
  CompanyPhoneTwo?: string;
  CompanyEmail?: string;
  CompanyWebsite?: string;
  CompanyFax?: string;
  DefaultCurrencyId: string;
  CompanyLogoPath?: string;
  ReportHeaderPath?: string;
  ReportFooterPath?: string;
  CompanyLogoHash?: string;
  ReportHeaderHash?: string;
  ReportFooterHash?: string;

  CompanyLogoBinaryData?: Uint8Array;
  ReportHeaderBinaryData?: Uint8Array;
  ReportFooterBinaryData?: Uint8Array;

 
 CompanyLogoContentType?: string;
 ReportHeaderContentType?: string;
 ReportFooterContentType?: string;

  AttachmentFolderPath?: string;
  AttachedFileMaxSize?: number;
  // Email Configuration
  IsEmailEnabled: boolean;
  EmailHost?: string;
  EmailPort?: number;
  EmailUsername?: string;
  EmailPassword?: string; // Masked on response
  SenderEmail?: string;
  EmailEnableSSL?: boolean;


  

}

// Update Setting Command - matches UpdateSettingRequest from backend
export interface UpdateSettingCommand {
  Id: string;
  RowVersion: string; // Base64 encoded byte array
  CompanyNameEn: string;
  CompanyNameAr?: string;
  CountryId?: string;
  CompanyAddress: string;
  CompanyPhoneOne: string;
  CompanyPhoneTwo?: string;
  CompanyEmail?: string;
  CompanyWebsite?: string;
  CompanyFax?: string;
  DefaultCurrencyId: string;
  DeleteLogo?: boolean;
  DeleteReportHeader?: boolean;
  DeleteReportFooter?: boolean;
}

// Update Setting Response - matches UpdateSettingResponse from backend
export interface UpdateSettingResponse {
  Message: string;
  Id: string;
}
