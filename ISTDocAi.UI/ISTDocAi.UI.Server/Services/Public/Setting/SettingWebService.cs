using ISTDocAi.Application.Services.Public.SettingService;
using ISTDocAi.Application.Services.Public.SettingService.Contracts;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.UI.Server.Helpers;
using ISTDocAi.UI.Server.Helpers.Models;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.UI.Server.Services.Public.Setting;

public class SettingWebService(IWebHostEnvironment env, ISettingService settingService) : ISettingWebService
{
    public async Task<Result<ImageResponse>> GetImage(ImageType imageType, CancellationToken cancellationToken)
    {
        var setting = await settingService.GetSettingValueAsync(
            x => new
            {
                x.CompanyLogoBinaryData,
                x.CompanyLogoContentType,
                x.ReportHeaderBinaryData,
                x.ReportHeaderContentType,
                x.ReportFooterBinaryData,
                x.ReportFooterContentType
            },
            cancellationToken);

        if (setting is null)
        {
            return Result.Failure<ImageResponse>(
                Error.NotFound(
                    ErrorCodes.SettingImageNotFound,
                    "Setting image not found."));
        }

        byte[]? binaryData;
        string? contentType;

        switch (imageType)
        {
            case ImageType.Logo:
                binaryData = setting.CompanyLogoBinaryData;
                contentType = setting.CompanyLogoContentType;
                break;
            case ImageType.ReportHeader:
                binaryData = setting.ReportHeaderBinaryData;
                contentType = setting.ReportHeaderContentType;
                break;
            case ImageType.ReportFooter:
                binaryData = setting.ReportFooterBinaryData;
                contentType = setting.ReportFooterContentType;
                break;
            default:
                return Result<ImageResponse>.Failure(Error.Validation(ErrorCodes.SettingInvalidImageType, "Invalid image type requested."));
        }

        if (binaryData is null || binaryData.Length == 0 || string.IsNullOrWhiteSpace(contentType))
        {
            return Result.Failure<ImageResponse>(
                Error.NotFound(
                    ErrorCodes.SettingImageNotFound,
                    "Setting image not found."));
        }

        return Result.Success(new ImageResponse
        {
            BinaryData = binaryData,
            ContentType = contentType,
            FileName = $"{imageType}{FileStorageHelper.GetFileExtension(contentType)}"
        });
    }

    public Task<Result<SettingResponse>> GetSettingAsync(CancellationToken cancellationToken)
    {
    
        return settingService.GetSettingAsync(cancellationToken);
    }

    public async Task<Result<UpdateSettingResponse>> UpdateSetting(UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        // 1) Get the current settings (paths + hashes)
        var currentResult = await settingService.GetSettingAsync(cancellationToken);
        if (!currentResult.IsSuccess)
            return Result<UpdateSettingResponse>.Failure(currentResult.Error);

        var current = currentResult.Value; // SettingResponse

        var relativeFolderPath = Path.Combine("uploads", "public", "settings","images");

        // Store old files that need to be deleted after successful DB update
        var filesToDeleteAfterSuccess = new List<string>();
        // Store new files that need to be deleted if something fails after DB update (since they were just uploaded)
        var filesToDeleteOnFailure = new List<string>();

        
        
        // Values to send to the Application layer (null means "no change")
        string? newLogoPath = null;
        string? newHeaderPath = null;
        string? newFooterPath = null;

        byte[]? newLogoBinaryData = null;
        byte[]? newHeaderBinaryData = null;
        byte[]? newFooterBinaryData = null;

        string? newLogoContentType = null;
        string? newHeaderContentType = null;
        string? newFooterContentType = null;


        string? newLogoHash = null;
        string? newHeaderHash = null;
        string? newFooterHash = null;

        bool deleteLogo = request.DeleteLogo;
        bool deleteHeader = request.DeleteReportHeader;
        bool deleteFooter = request.DeleteReportFooter;

        // 2) Conflicts check (optional but important for clarity)
        if (deleteLogo && request.LogoFile is not null)
            return Result<UpdateSettingResponse>.Failure(Error.BusinessRule(ErrorCodes.SettingLogoConflict, "Cannot upload and delete logo at the same time."));

        if (deleteHeader && request.ReportHeaderFile is not null)
            return Result<UpdateSettingResponse>.Failure(Error.BusinessRule(ErrorCodes.SettingHeaderConflict, "Cannot upload and delete header at the same time."));

        if (deleteFooter && request.ReportFooterFile is not null)
            return Result<UpdateSettingResponse>.Failure(Error.BusinessRule(ErrorCodes.SettingFooterConflict, "Cannot upload and delete footer at the same time."));

        // ===== Logo =====
        if (deleteLogo)
        {
            //newLogoPath = "";   // Will be interpreted in Application as delete (or use flag only)
            //newLogoHash = "";   // Same concept
            if (!string.IsNullOrWhiteSpace(current.CompanyLogoPath))
                filesToDeleteAfterSuccess.Add(current.CompanyLogoPath);
        }
        else if (request.LogoFile is not null)
        {
            var uploadedHash = await FileStorageHelper.ComputeSha256Async(request.LogoFile, cancellationToken);
            var currentFileExists = !string.IsNullOrWhiteSpace(current.CompanyLogoPath) && 
                                   File.Exists(Path.Combine(env.WebRootPath, current.CompanyLogoPath));
            
            // Save new file if: different hash OR file missing from disk
            if (!currentFileExists || !string.Equals(uploadedHash, current.CompanyLogoHash, StringComparison.OrdinalIgnoreCase) || (current.CompanyLogoBinaryData is null && current.CompanyLogoContentType is null))
            {
                newLogoPath = await FileStorageHelper.SaveFileAsync(request.LogoFile, env.WebRootPath, relativeFolderPath, "logo");
                filesToDeleteOnFailure.Add(newLogoPath);
                newLogoHash = uploadedHash;
                //work around to avoid re-reading the file in the Application layer just to get its binary data for DB storage (since we already have it in memory here)
                newLogoBinaryData = await FileStorageHelper.ConvertFileToMemoryStream(request.LogoFile, cancellationToken);
                newLogoContentType = request.LogoFile.ContentType;

                if (!string.IsNullOrWhiteSpace(current.CompanyLogoPath))
                    filesToDeleteAfterSuccess.Add(current.CompanyLogoPath);
            }
            // else: Same file → no change (keep it null)
        }

        // ===== Header =====
        if (deleteHeader)
        {
            //newHeaderPath = "";
            //newHeaderHash = "";
            if (!string.IsNullOrWhiteSpace(current.ReportHeaderPath))
                filesToDeleteAfterSuccess.Add(current.ReportHeaderPath);
        }
        else if (request.ReportHeaderFile is not null)
        {
            var uploadedHash = await FileStorageHelper.ComputeSha256Async(request.ReportHeaderFile, cancellationToken);
            var currentFileExists = !string.IsNullOrWhiteSpace(current.ReportHeaderPath) && 
                                   File.Exists(Path.Combine(env.WebRootPath, current.ReportHeaderPath));
            
            // Save new file if: different hash OR file missing from disk
            if (!currentFileExists || !string.Equals(uploadedHash, current.ReportHeaderHash, StringComparison.OrdinalIgnoreCase) || ( current.ReportHeaderBinaryData is null && current.ReportHeaderContentType is null))
            {
                newHeaderPath = await FileStorageHelper.SaveFileAsync(request.ReportHeaderFile, env.WebRootPath, relativeFolderPath, "ReportHeader"); 
                filesToDeleteOnFailure.Add(newHeaderPath);
                newHeaderHash = uploadedHash;

                //work around to avoid re-reading the file in the Application layer just to get its binary data for DB storage (since we already have it in memory here)
                newHeaderBinaryData = await FileStorageHelper.ConvertFileToMemoryStream(request.ReportHeaderFile, cancellationToken);
                newHeaderContentType = request.ReportHeaderFile.ContentType;

                if (!string.IsNullOrWhiteSpace(current.ReportHeaderPath))
                    filesToDeleteAfterSuccess.Add(current.ReportHeaderPath);
            }
        }

        // ===== Footer =====
        if (deleteFooter)
        {
            //newFooterPath = "";
            //newFooterHash = "";
            if (!string.IsNullOrWhiteSpace(current.ReportFooterPath))
                filesToDeleteAfterSuccess.Add(current.ReportFooterPath);
        }
        else if (request.ReportFooterFile is not null)
        {
            var uploadedHash = await FileStorageHelper.ComputeSha256Async(request.ReportFooterFile, cancellationToken);
            var currentFileExists = !string.IsNullOrWhiteSpace(current.ReportFooterPath) && 
                                   File.Exists(Path.Combine(env.WebRootPath, current.ReportFooterPath));
            
            // Save new file if: different hash OR file missing from disk
            if (!currentFileExists || !string.Equals(uploadedHash, current.ReportFooterHash, StringComparison.OrdinalIgnoreCase) || (current.ReportFooterBinaryData is null && current.ReportFooterContentType is null))
            {
                newFooterPath = await FileStorageHelper.SaveFileAsync(request.ReportFooterFile, env.WebRootPath, relativeFolderPath, "ReportFooter"); 
                filesToDeleteOnFailure.Add(newFooterPath);
                newFooterHash = uploadedHash;
                //work around to avoid re-reading the file in the Application layer just to get its binary data for DB storage (since we already have it in memory here)
                newFooterBinaryData = await FileStorageHelper.ConvertFileToMemoryStream(request.ReportFooterFile, cancellationToken);
                newFooterContentType = request.ReportFooterFile.ContentType;


                if (!string.IsNullOrWhiteSpace(current.ReportFooterPath))
                    filesToDeleteAfterSuccess.Add(current.ReportFooterPath);
            }
        }

        // 3) Build UpdateSetting (Application DTO)
        var updateSetting = new UpdateSetting
        {
            Id = request.Id,
            RowVersion = request.RowVersion,
            CompanyNameEn = request.CompanyNameEn,
            CompanyNameAr = request.CompanyNameAr,
            CountryId = request.CountryId,
            CompanyAddress = request.CompanyAddress,
            CompanyPhoneOne = request.CompanyPhoneOne,
            CompanyPhoneTwo = request.CompanyPhoneTwo,
            CompanyEmail = request.CompanyEmail,
            CompanyWebsite = request.CompanyWebsite,
            CompanyFax = request.CompanyFax,
            DefaultCurrencyId = request.DefaultCurrencyId,

            // flags
            DeleteLogo = deleteLogo,
            DeleteReportHeader = deleteHeader,
            DeleteReportFooter = deleteFooter,

            // paths/hashes (null = no change)
            CompanyLogoPath = newLogoPath,
            ReportHeaderPath = newHeaderPath,
            ReportFooterPath = newFooterPath,
            // Binary Data (null = no change)
            CompanyLogoBinaryData = newLogoBinaryData,
            ReportHeaderBinaryData = newHeaderBinaryData,
            ReportFooterBinaryData = newFooterBinaryData,
            // Content Types (null = no change)
            CompanyLogoContentType = newLogoContentType,
            ReportHeaderContentType = newHeaderContentType,
            ReportFooterContentType = newFooterContentType,

            CompanyLogoHash = newLogoHash,
            ReportHeaderHash = newHeaderHash,
            ReportFooterHash = newFooterHash,

            AttachmentFolderPath = request.AttachmentFolderPath,
            AttachedFileMaxSize = request.AttachedFileMaxSize,

            // Email Configuration
            IsEmailEnabled = request.IsEmailEnabled,
            EmailHost = request.EmailHost,
            EmailPort = request.EmailPort,
            EmailUsername = request.EmailUsername,
            EmailPassword = request.EmailPassword,
            SenderEmail = request.SenderEmail,
            EmailEnableSSL = request.EmailEnableSSL,




        };

        var result = await settingService.UpdateSetting(updateSetting, cancellationToken);
        if (!result.IsSuccess)
        {
            foreach (var rel in filesToDeleteOnFailure.Distinct())
                FileStorageHelper.TryDeletePhysicalFile(env.WebRootPath, rel);
            return result;
        } 
            
      

        // 4) After successful DB update: delete old files best-effort
        foreach (var rel in filesToDeleteAfterSuccess.Distinct())
            FileStorageHelper.TryDeletePhysicalFile(env.WebRootPath, rel);

        return result;
    }

}
