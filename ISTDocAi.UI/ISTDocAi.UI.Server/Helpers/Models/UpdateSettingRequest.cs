namespace ISTDocAi.UI.Server.Helpers.Models;

public record UpdateSettingRequest
{
    public string Id { get; init; }

    public byte[] RowVersion { get; set; }

    public string CompanyNameEn { get; set; }

    public string? CompanyNameAr { get; set; }

    public string? CountryId { get; set; }

   
    public string CompanyAddress { get; set; }

    public string CompanyPhoneOne { get; set; }

    public string? CompanyPhoneTwo { get; set; }


    public string? CompanyEmail { get; set; }

    public string? CompanyWebsite { get; set; }

    public string? CompanyFax { get; set; }


    public string DefaultCurrencyId { get; set; }

    // Files
    public IFormFile? LogoFile { get; set; }
    public IFormFile? ReportHeaderFile { get; set; }
    public IFormFile? ReportFooterFile { get; set; }


    public bool DeleteLogo { get; set; }=false;
    public bool DeleteReportHeader { get; set; } =false;
    public bool DeleteReportFooter { get; set; } = false;

    public string? AttachmentFolderPath { get; set; }
    public int? AttachedFileMaxSize { get; set; }

    // Email Configuration
    public bool IsEmailEnabled { get; set; } = false;
    public string? EmailHost { get; set; }
    public int? EmailPort { get; set; }
    public string? EmailUsername { get; set; }
    public string? EmailPassword { get; set; }
    public string? SenderEmail { get; set; }
    public bool EmailEnableSSL { get; set; } = true;

  

}
