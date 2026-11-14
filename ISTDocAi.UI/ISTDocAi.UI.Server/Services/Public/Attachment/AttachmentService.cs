using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentAdd;
using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentDelete;
using ISTDocAi.Application.Features.Public.Attachment.Commands.AttachmentUpdate;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetById;
using ISTDocAi.Application.Features.Public.Attachment.Queries.AttachmentGetListByReferenceIdAndReferenceType;
using ISTDocAi.Application.Services.Public.SettingService;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ISTDocAi.UI.Server.Helpers;
using ISTDocAi.UI.Server.Services.Public.Attachment.Contracts;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.UI.Server.Services.Public.Attachment
{
    public sealed class AttachmentService(
    ISender mediator,
    ISettingService settingService,
    TimeProvider timeProvider
) : IAttachmentService
    {
        private async Task<(string Root, long MaxBytes)> GetAttachmentStorageConfigAsync(CancellationToken ct)
        {
            var settingResult = await settingService.GetSettingValueAsync(x=> new { x.AttachmentFolderPath,x.AttachedFileMaxSize },ct);
            if (settingResult is null)
                throw new InvalidOperationException("Cannot load system settings.");

            var setting = settingResult;

            if (string.IsNullOrWhiteSpace(setting.AttachmentFolderPath))
                throw new InvalidOperationException("AttachmentFolderPath is not configured in settings.");

            // MB -> bytes, default 10MB if null (optional)
            var maxMb = setting.AttachedFileMaxSize ?? 10;
            if (maxMb < 1) maxMb = 1;

            long maxBytes = maxMb * 1024L * 1024L;
            return (setting.AttachmentFolderPath!, maxBytes);
        }

        private static void ValidateFile(IFormFile file, long maxBytes)
        {
            if (file is null || file.Length == 0)
                throw new InvalidOperationException("File is required.");

            if (file.Length > maxBytes)
                throw new InvalidOperationException($"File size exceeds the allowed maximum ({maxBytes / (1024 * 1024)} MB).");
        }

        public async Task<Result<AttachmentAddCommandResponse>> AddAsync(AttachmentAddRequest request, CancellationToken ct)
        {
            try
            {
                var (root, maxBytes) = await GetAttachmentStorageConfigAsync(ct);
                ValidateFile(request.File, maxBytes);
             
                // generate stored name
                var storedFileName = FileStorageHelper.GenerateStoredFileName(request.OriginalFileName);

                // relative path in DB
                var relativePath = FileStorageHelper.BuildRelativePath(timeProvider.GetLocalNow().Year.ToString(),request.ReferenceType, request.ReferenceSerial, storedFileName);

                // full path on disk
                var fullPath = FileStorageHelper.CombineRootAndRelative(root, relativePath);

                // save first (Physical)
                await FileStorageHelper.SaveFileAsync(request.File, fullPath, ct);

                // compute hash after save (or before)
                var hash = await FileStorageHelper.ComputeSha256Async(request.File, ct);

                var cmd = new AttachmentAddCommand(
                    StoredFileName: storedFileName,
                    OriginalFileName: request.OriginalFileName,
                    FilePath: relativePath,                    // ✅ relative in DB
                    ContentType: request.File.ContentType,
                    FileExtension: Path.GetExtension(request.OriginalFileName),
                    FileHash: hash,
                    FileSizeInBytes: request.File.Length,
                    Note: request.Note,
                    ReferenceType: request.ReferenceType,
                    ReferenceId: request.ReferenceId,
                    ReferenceSerial: request.ReferenceSerial
                );

                var dbResult = await mediator.Send(cmd, ct);

                // If DB fails → delete the physical file that was created
                if (!dbResult.IsSuccess)
                    FileStorageHelper.TryDeletePhysicalFile(fullPath);

                return dbResult;
            }
            catch(Exception e)
            {
                return Result.Failure<AttachmentAddCommandResponse>(Error.Validation(ErrorCodes.AttachmentAddInvalid, e.Message));
            }
           

          
        }

        public async Task<Result<AttachmentUpdateCommandResponse>> UpdateAsync(AttachmentUpdateRequest request, CancellationToken ct)
        {
            // 1) get old attachment (to know old file path)
            var oldResult = await mediator.Send(new AttachmentGetByIdQuery(request.Id), ct);
            if (!oldResult.IsSuccess)
                return Result<AttachmentUpdateCommandResponse>.Failure(oldResult.Error);

            var old = oldResult.Value;

            var (root, maxBytes) = await GetAttachmentStorageConfigAsync(ct);

            string? newRelativePath = null;
            string? newStoredFileName = null;
            string? newHash = null;
            long newSize = old.FileSizeInBytes;
            string? newContentType = old.ContentType;
            string? newExt = old.FileExtension;

            string? newFullPath = null;

            // 2) if file provided → save new physical first
            if (request.File is not null)
            {
                ValidateFile(request.File, maxBytes);

                newStoredFileName = FileStorageHelper.GenerateStoredFileName(request.OriginalFileName);
                newRelativePath = FileStorageHelper.BuildRelativePath(timeProvider.GetLocalNow().Year.ToString(),request.ReferenceType, request.ReferenceSerial, newStoredFileName);
                newFullPath = FileStorageHelper.CombineRootAndRelative(root, newRelativePath);

                await FileStorageHelper.SaveFileAsync(request.File, newFullPath, ct);

                newHash = await FileStorageHelper.ComputeSha256Async(request.File, ct);
                newSize = request.File.Length;
                newContentType = request.File.ContentType;
                newExt = Path.GetExtension(request.OriginalFileName);
            }
            else
            {
                // metadata only: keep old stored/filepath/hash/size unless you want change original name only
                newStoredFileName = old.StoredFileName;
                newRelativePath = old.FilePath;
                newHash = old.FileHash;
            }

            // 3) Update DB
            var cmd = new AttachmentUpdateCommand(
                Id: request.Id,
                StoredFileName: newStoredFileName!,
                OriginalFileName: request.OriginalFileName,
                FilePath: newRelativePath!,
                ContentType: newContentType,
                FileExtension: newExt,
                FileHash: newHash,
                FileSizeInBytes: newSize,
                Note: request.Note,
                ReferenceType: request.ReferenceType,
                ReferenceId: request.ReferenceId,
                ReferenceSerial: request.ReferenceSerial
            )
            {
                RowVersion = request.RowVersion
            };

            var dbResult = await mediator.Send(cmd, ct);

            // 4) Physical delete policy:
            // DB succeeds first → then delete the old one (if there was a new file)
            if (dbResult.IsSuccess && request.File is not null)
            {
                var oldFullPath = FileStorageHelper.CombineRootAndRelative(root, old.FilePath);
                FileStorageHelper.TryDeletePhysicalFile(oldFullPath);
            }

            // If DB fails and you have a new file → delete the new one so it doesn't become orphan
            if (!dbResult.IsSuccess && request.File is not null && newFullPath is not null)
            {
                FileStorageHelper.TryDeletePhysicalFile(newFullPath);
            }

            return dbResult;
        }

        public async Task<Result<AttachmentDeleteCommandResponse>> DeleteAsync(AttachmentDeleteRequest request, CancellationToken ct)
        {
            if (request.Items is null || request.Items.Count == 0)
                return Result<AttachmentDeleteCommandResponse>.Failure(Error.Validation(ErrorCodes.AttachmentDeleteInvalid, "No items provided."));

            var ids = request.Items.Select(x => x.Id).Distinct().ToList();

            // 1) read attachments first (to know file paths for physical deletion later)
            // NOTE: There's no Query Batch here, so we'll get them one by one
            var (root, _) = await GetAttachmentStorageConfigAsync(ct);

            var pathsById = new Dictionary<string, string>();
            foreach (var id in ids)
            {
                var get = await mediator.Send(new AttachmentGetByIdQuery(id), ct);
                if (get.IsSuccess)
                {
                    var full = FileStorageHelper.CombineRootAndRelative(root, get.Value.FilePath);
                    pathsById[id] = full;
                }
            }

            // 2) Hard-delete database rows first.
            var cmd = new AttachmentDeleteCommand(request.Items);
            var dbResult = await mediator.Send(cmd, ct);

            // 3) Delete files only for IDs confirmed as deleted by the database operation.
            if (dbResult.IsSuccess && dbResult.Value.DeletedIds?.Count > 0)
            {
                foreach (var deletedId in dbResult.Value.DeletedIds)
                {
                    if (pathsById.TryGetValue(deletedId, out var fullPath))
                        FileStorageHelper.TryDeletePhysicalFile(fullPath);
                }
            }

            return dbResult;
        }

        public async Task<Result<FileStreamResult>> GetFileAsync(string id, bool forceDownload, CancellationToken ct)
        {
            try
            {
                var get = await mediator.Send(new AttachmentGetByIdQuery(id), ct);
                if (!get.IsSuccess)
                    return Result.Failure<FileStreamResult>(get.Error);

                var attachment = get.Value;

                var (root, _) = await GetAttachmentStorageConfigAsync(ct);

                var fullPath = FileStorageHelper.CombineRootAndRelative(root, attachment.FilePath);

                if (!System.IO.File.Exists(fullPath))
                    return Result.Failure<FileStreamResult>(Error.NotFound(ErrorCodes.AttachmentFileNotFound, "Physical file not found."));

                var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType;

                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var fileResult = new FileStreamResult(stream, contentType);

                // name
                fileResult.FileDownloadName = attachment.OriginalFileName;

                // ✅ IMPORTANT:
                // - If you set FileDownloadName ONLY: ASP.NET Core may emit attachment disposition in some cases.
                // - We will control disposition explicitly using headers. Need HttpContext.
                // So we return result + tell controller to set header.
                // Easiest: set header in controller before returning fileResult.

                // We'll store an indicator via HttpContext? not here.
                // Instead do header in controller (recommended).

                return Result.Success(fileResult);
            }
            catch (Exception e)
            {
                return Result.Failure<FileStreamResult>(Error.Unexpected(ErrorCodes.AttachmentFileError, e.Message));
            }
        }


        public async Task<Result<AttachmentGetByIdQueryResponse>> GetByIdAsync(string id, CancellationToken ct)
            => await mediator.Send(new AttachmentGetByIdQuery(id), ct);

        public async Task<Result<List<AttachmentGetListByReferenceIdAndReferenceTypeQueryResponse>>> GetListAsync(string referenceType, string referenceId, CancellationToken ct)
            => await mediator.Send(new AttachmentGetListByReferenceIdAndReferenceTypeQuery(referenceId, referenceType), ct);
    }
}

