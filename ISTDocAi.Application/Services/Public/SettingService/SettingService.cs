using MapsterMapper;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Entities.Public;
using System.Linq.Expressions;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Services;
using ISTDocAi.Application.Services.Public.SettingService.Contracts;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Application.Services.Public.SettingService;

public class SettingService(
    IRepositoryOp<Pub_Setting> settingRepoOp,
    IRepository<Pub_Setting> settingRepo,
    IMapper mapper,
    IPasswordEncryptionService passwordEncryption,
    IActionLogService actionLog,
    IUnitOfWork unitOfWork
    ) : ISettingService
{
    public async Task<Result<SettingResponse>> GetSettingAsync(CancellationToken cancellationToken)
    {
        var settings = await settingRepoOp.GetListAsyncIncludes(
            predicate: s => true,
            includes: null,
            cancellationToken: cancellationToken
        );


        if (settings == null || settings.Items.Count() == 0)
        {
            return Result<SettingResponse>.Failure(Error.NotFound(ErrorCodes.SettingNotFound, "No setting record found."));
        }
        var setting = settings.Items.FirstOrDefault();
        var settingResponse = mapper.Map<SettingResponse>(setting);
        
        // Mask password for security
        if (!string.IsNullOrEmpty(settingResponse.EmailPassword))
        {
            settingResponse.EmailPassword = EmailConfiguration.MASKED_PASSWORD;
        }
        
        return Result.Success(settingResponse);
    }

    public async Task<Result<EmailConfiguration>> GetEmailConfigurationAsync(CancellationToken cancellationToken)
    {
        var setting = await settingRepoOp.GetFirstAsync(cancellationToken);

        if (setting is null)
        {
            return Result<EmailConfiguration>.Failure(Error.NotFound(ErrorCodes.SettingNotFound, "Email configuration not found."));
        }

        if (!setting.IsEmailEnabled)
        {
            return Result<EmailConfiguration>.Failure(Error.BusinessRule(ErrorCodes.EmailDisabled, "Email notifications are disabled."));
        }

        if (string.IsNullOrEmpty(setting.EmailHost) || string.IsNullOrEmpty(setting.EmailUsername))
        {
            return Result<EmailConfiguration>.Failure(Error.Validation(ErrorCodes.EmailIncomplete, "Email configuration is incomplete."));
        }

        string decryptedPassword = string.Empty;
        if (!string.IsNullOrEmpty(setting.EmailPassword))
        {
            try
            {
                decryptedPassword = passwordEncryption.Decrypt(setting.EmailPassword);
            }
            catch (Exception)
            {
                return Result<EmailConfiguration>.Failure(Error.Unexpected(ErrorCodes.EmailDecryptionFailed, "Failed to decrypt email password"));
            }
        }

        var emailConfig = new EmailConfiguration
        {
            IsEmailEnabled = setting.IsEmailEnabled,
            EmailHost = setting.EmailHost,
            EmailPort = setting.EmailPort ?? 587,
            EmailUsername = setting.EmailUsername,
            DecryptedPassword = decryptedPassword,
            SenderEmail = setting.SenderEmail ?? setting.EmailUsername,
            EmailEnableSSL = setting.EmailEnableSSL
        };

        return Result.Success(emailConfig);
    }

    public async Task<bool> IsEmailEnabledAsync(CancellationToken cancellationToken)
    {
        var setting = await settingRepoOp.GetFirstAsync(cancellationToken);

        return setting?.IsEmailEnabled ?? false;
    }

    public async Task<Result<UpdateSettingResponse>> UpdateSetting(UpdateSetting request, CancellationToken cancellationToken)
    {
        var settingToUpdate = await settingRepo.GetAsync(request.Id, cancellationToken: cancellationToken); 

        if (settingToUpdate == null)
        {
            return Result<UpdateSettingResponse>.Failure(Error.NotFound(ErrorCodes.SettingNotFound, "The setting to update was not found."));

        }

        if (request.AttachedFileMaxSize.HasValue && request.AttachedFileMaxSize < 1)
        {
            return Result<UpdateSettingResponse>.Failure(
                Error.Validation(ErrorCodes.SettingInvalid, "One or more system setting values are invalid."));
        }

        if (request.AttachmentFolderPath != null &&
            string.IsNullOrWhiteSpace(request.AttachmentFolderPath))
        {
            return Result<UpdateSettingResponse>.Failure(
                Error.Validation(ErrorCodes.SettingInvalid, "One or more system setting values are invalid."));
        }

        request.EmailHost = request.EmailHost?.Trim();
        request.EmailUsername = request.EmailUsername?.Trim();
        request.SenderEmail = request.SenderEmail?.Trim();


        var oldLogoPath = settingToUpdate.CompanyLogoPath;
        var oldLogoHash = settingToUpdate.CompanyLogoHash;
        var oldReportHeaderPath = settingToUpdate.ReportHeaderPath;
        var oldReportHeaderHash = settingToUpdate.ReportHeaderHash;
        var oldReportFooterPath = settingToUpdate.ReportFooterPath;
        var oldReportFooterHash = settingToUpdate.ReportFooterHash;
        var oldLogoBinaryData = settingToUpdate.CompanyLogoBinaryData;
        var oldReportHeaderBinaryData = settingToUpdate.ReportHeaderBinaryData;
        var oldReportFooterBinaryData = settingToUpdate.ReportFooterBinaryData;
        var oldLogoContentType = settingToUpdate.CompanyLogoContentType;
        var oldReportHeaderContentType = settingToUpdate.ReportHeaderContentType;
        var oldReportFooterContentType = settingToUpdate.ReportFooterContentType;
        var oldEmailPassword = settingToUpdate.EmailPassword; // Preserve existing encrypted password

        mapper.Map(request, settingToUpdate);


        // Restore/Apply image logic
        if (request.DeleteLogo)
        {
            settingToUpdate.CompanyLogoPath = null;
            settingToUpdate.CompanyLogoHash = null;
            settingToUpdate.CompanyLogoBinaryData = null;
            settingToUpdate.CompanyLogoContentType = null;
        }
        else if (request.CompanyLogoBinaryData is not null && request.CompanyLogoContentType is not null)
        {
            settingToUpdate.CompanyLogoPath = request.CompanyLogoPath;
            settingToUpdate.CompanyLogoHash = request.CompanyLogoHash;
            // If new logo path is provided, we assume new binary data and content type are also provided in the request
            settingToUpdate.CompanyLogoBinaryData = request.CompanyLogoBinaryData;
            settingToUpdate.CompanyLogoContentType = request.CompanyLogoContentType;
        }
        else
        {
            settingToUpdate.CompanyLogoPath = oldLogoPath;
            settingToUpdate.CompanyLogoHash = oldLogoHash;
            settingToUpdate.CompanyLogoBinaryData = oldLogoBinaryData;
            settingToUpdate.CompanyLogoContentType = oldLogoContentType;
        }



        if (request.DeleteReportHeader)
        {
            settingToUpdate.ReportHeaderPath = null;
            settingToUpdate.ReportHeaderHash = null;
            settingToUpdate.ReportHeaderBinaryData = null;
            settingToUpdate.ReportHeaderContentType = null;

        }
        else if (request.ReportHeaderBinaryData is not null && request.ReportHeaderContentType is not null)
        {
            settingToUpdate.ReportHeaderPath = request.ReportHeaderPath;
            settingToUpdate.ReportHeaderHash = request.ReportHeaderHash;
            // If new report header path is provided, we assume new binary data and content type are also provided in the request
            settingToUpdate.ReportHeaderBinaryData = request.ReportHeaderBinaryData;
            settingToUpdate.ReportHeaderContentType = request.ReportHeaderContentType;
        }
        else
        {
            settingToUpdate.ReportHeaderPath = oldReportHeaderPath;
            settingToUpdate.ReportHeaderHash = oldReportHeaderHash;
            settingToUpdate.ReportHeaderBinaryData = oldReportHeaderBinaryData;
            settingToUpdate.ReportHeaderContentType = oldReportHeaderContentType;
        }


        if (request.DeleteReportFooter)
        {
            settingToUpdate.ReportFooterPath = null;
            settingToUpdate.ReportFooterHash = null;
            settingToUpdate.ReportFooterBinaryData = null;
            settingToUpdate.ReportFooterContentType = null;
        }
        else if (request.ReportFooterBinaryData is not null && request.ReportFooterContentType is not null)
        {
            settingToUpdate.ReportFooterPath = request.ReportFooterPath;
            settingToUpdate.ReportFooterHash = request.ReportFooterHash;
            // If new report footer path is provided, we assume new binary data and content type are also provided in the request
            settingToUpdate.ReportFooterBinaryData = request.ReportFooterBinaryData;
            settingToUpdate.ReportFooterContentType = request.ReportFooterContentType;
        }
        else
        {
            settingToUpdate.ReportFooterPath = oldReportFooterPath;
            settingToUpdate.ReportFooterHash = oldReportFooterHash;
            settingToUpdate.ReportFooterBinaryData = oldReportFooterBinaryData;
            settingToUpdate.ReportFooterContentType = oldReportFooterContentType;
        }

        // Encrypt email password if provided
        if (!string.IsNullOrEmpty(request.EmailPassword) && request.EmailPassword != EmailConfiguration.MASKED_PASSWORD)
        {
            settingToUpdate.EmailPassword = passwordEncryption.Encrypt(request.EmailPassword);
        }
        else
        {
            settingToUpdate.EmailPassword = oldEmailPassword;
        }

        // Validate email configuration if enabled
        if (request.IsEmailEnabled)
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.EmailHost))
                validationErrors.Add("Email Host is required when email is enabled");
            
            if (!request.EmailPort.HasValue || request.EmailPort < 1 || request.EmailPort > 65535)
                validationErrors.Add("Valid Email Port (1-65535) is required when email is enabled");
            
            if (string.IsNullOrWhiteSpace(request.EmailUsername))
                validationErrors.Add("Email Username is required when email is enabled");
            
            if (string.IsNullOrWhiteSpace(request.EmailPassword) && string.IsNullOrWhiteSpace(settingToUpdate.EmailPassword))
                validationErrors.Add("Email Password is required when email is enabled");
            
            if (string.IsNullOrWhiteSpace(request.SenderEmail))
                validationErrors.Add("Sender Email is required when email is enabled");

            if (validationErrors.Any())
            {
                return Result<UpdateSettingResponse>.Failure(Error.Validation(ErrorCodes.EmailValidationFailed, string.Join("; ", validationErrors)));
            }
        }

        await settingRepo.UpdateAsync(settingToUpdate, cancellationToken);



        await actionLog.LogAction(Domain.Common.Enums.Pub_ActionTypeEnum.Pub_SettingUpdate, settingToUpdate.Id, null, settingToUpdate, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UpdateSettingResponse
        (
           settingToUpdate.Id,
           "Setting updated successfully."
        ));
    }

    public async Task<TValue?> GetSettingValueAsync<TValue>(Expression<Func<Pub_Setting, TValue>> selector, CancellationToken cancellationToken)
    {
        var value = await settingRepoOp.GetFirstAsync<TValue>(x => true, selector, cancellationToken);
     
        return value;
    }
}

