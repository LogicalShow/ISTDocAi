using ISTDocAi.Domain.Common.Attributes;
using ISTDocAi.Domain.Common.Entities;

namespace ISTDocAi.Domain.Entities.Public;

[ActionSeeder]
public class Pub_Setting : AuditableEntity
{
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

    public string? CompanyLogoPath { get; set; }

    public string? ReportHeaderPath { get; set; }

    public string? ReportFooterPath { get; set; }

    public byte[]? CompanyLogoBinaryData { get; set; }

    public byte[]? ReportHeaderBinaryData { get; set; }
    public byte[]? ReportFooterBinaryData { get; set; }

    public string? CompanyLogoContentType { get; set; }
    public string? ReportHeaderContentType { get; set; }
    public string? ReportFooterContentType { get; set; }
    public string? CompanyLogoHash { get; set; }
    public string? ReportHeaderHash { get; set; }
    public string? ReportFooterHash { get; set; }

    public string? AttachmentFolderPath { get; set; }
    public int? AttachedFileMaxSize { get; set; }

    // Email Configuration
    public bool IsEmailEnabled { get; set; }
    public string? EmailHost { get; set; }
    public int? EmailPort { get; set; }
    public string? EmailUsername { get; set; }
    public string? EmailPassword { get; set; } // Encrypted
    public string? SenderEmail { get; set; }
    public bool EmailEnableSSL { get; set; } = true;




}
