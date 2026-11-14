using System.Security.Cryptography;

namespace ISTDocAi.UI.Server.Helpers
{
    public static class FileStorageHelper
    {
        public static async Task<string> SaveFileAsync(IFormFile file, string webRootPath, string relativeFolder, string prefix)
        {
            var folderOnDisk = Path.Combine(webRootPath, relativeFolder);
            Directory.CreateDirectory(folderOnDisk);
            var Random = new Random();
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{prefix}_{Random.Next()}{extension}";
            var fullPath = Path.Combine(folderOnDisk, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            // URL path (forward slashes)
            var cleanFolder = relativeFolder.TrimStart('/', '\\');
            return $"/{cleanFolder.Replace("\\", "/")}/{fileName}";
        }

        public static async Task<string> ComputeSha256Async(IFormFile file, CancellationToken ct = default)
        {
            await using var stream = file.OpenReadStream();
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hashBytes); // uppercase hex
        }

        public static void TryDeletePhysicalFile(string webRootPath, string relativeUrlPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativeUrlPath)) return;


                var rel = relativeUrlPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
                var full = Path.Combine(webRootPath, rel);
                if (File.Exists(full)) File.Delete(full);
            }
            catch
            {
                // log only 
            }
        }



        public static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return "NA";

            // remove invalid chars for Windows paths
            foreach (var c in Path.GetInvalidFileNameChars())
                segment = segment.Replace(c, '_');

            // extra safety for slashes
            segment = segment.Replace('/', '_').Replace('\\', '_').Trim();
            return string.IsNullOrWhiteSpace(segment) ? "NA" : segment;
        }

        public static string GenerateStoredFileName(string originalFileName)
        {
            var Random = new Random();
            //remove extension from originalFileName if any
            var originalNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var ext = Path.GetExtension(originalFileName);
            return $"{originalNameWithoutExt}_{Random.Next()}{ext}";
        }

        public static string BuildRelativePath(string year, string referenceType, string referenceSerial, string storedFileName)
        {
            var y = SanitizePathSegment(year);
            var rt = SanitizePathSegment(referenceType);
            var rs = SanitizePathSegment(referenceSerial);
            var sf = SanitizePathSegment(storedFileName);

            // store in DB using forward slashes
            return $"{y}/{rt}/{rs}/{sf}";
        }

        public static string CombineRootAndRelative(string rootAbsoluteOrUnc, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(rootAbsoluteOrUnc))
                throw new InvalidOperationException("Attachment root folder is not configured.");

            // normalize relative for OS path
            relativePath = relativePath
                .TrimStart('/', '\\')
                .Replace("/", Path.DirectorySeparatorChar.ToString())
                .Replace("\\", Path.DirectorySeparatorChar.ToString());

            return Path.Combine(rootAbsoluteOrUnc, relativePath);
        }


        public static async Task SaveFileAsync(IFormFile file, string fullPath, CancellationToken ct)
        {
            var folder = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(folder);

            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream, ct);
        }



        public static void TryDeletePhysicalFile(string fullPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullPath)) return;
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch
            {
                // log only (لو عندك ILogger في service اعمل Log بدل swallow)
            }
        }

        public static async Task<byte[]> ConvertFileToMemoryStream(IFormFile file, CancellationToken ct)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        public static string GetFileExtension(string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
    }
}
