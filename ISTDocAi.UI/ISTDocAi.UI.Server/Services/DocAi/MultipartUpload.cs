namespace ISTDocAi.UI.Server.Services.DocAi;
using System.Security.Cryptography;
using System.Text;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

// Read the network body once; hash and persist only the file part in that same pass.
public static class MultipartUpload
{
    public static async Task<PreparedSubmission> ReadAsync(HttpRequest request, string path, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var media) || !media.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)) throw IntakeException.Invalid();
        var boundary = HeaderUtilities.RemoveQuotes(media.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > 128) throw IntakeException.Invalid();
        var reader = new MultipartReader(boundary, request.Body) { HeadersCountLimit = 16, HeadersLengthLimit = 8192 };
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        bool found = false;
        int size = 0;
        string fileName = "";
        byte[] digest = [];
        int sections = 0;
        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
        {
            if (++sections > 6 || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || !disposition.DispositionType.Equals("form-data")) throw IntakeException.Invalid();
            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
            if (name is null) throw IntakeException.Invalid();
            bool isFile = disposition.FileName.HasValue || disposition.FileNameStar.HasValue;
            if (isFile)
            {
                if (name != "file" || found) throw IntakeException.Invalid();
                found = true;
                fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value ?? "";
                if (fileName.Length > 260) throw IntakeException.Invalid();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var output = File.OpenWrite(path);
                var buffer = new byte[81920];
                int read;
                while ((read = await section.Body.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > ISTDocAi.UI.Server.Controllers.DocAi.Contracts.Jobs.SubmitJobRequest.MaxFileBytes) throw new IntakeException(413, "FILE_TOO_LARGE", "File too large", "The file exceeds 20 MB.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                digest = hash.GetHashAndReset();
            }
            else
            {
                if (name is not ("documentType" or "shippingLine" or "clientRequestId" or "reprocess") || fields.ContainsKey(name)) throw IntakeException.Invalid();
                using var content = new MemoryStream();
                var buffer = new byte[1024];
                int read;
                while ((read = await section.Body.ReadAsync(buffer, ct)) > 0)
                {
                    if (content.Length + read > 1024) throw IntakeException.Invalid();
                    content.Write(buffer, 0, read);
                }
                fields.Add(name, Encoding.UTF8.GetString(content.ToArray()).Trim());
            }
        }
        string Field(string name, int max, bool required = false)
        {
            var value = fields.GetValueOrDefault(name, "");
            if (value.Length > max || (required && value.Length == 0) || value.Any(char.IsControl)) throw IntakeException.Invalid();
            return value;
        }
        if (!found || size == 0) throw IntakeException.Invalid();
        var type = Field("documentType", 20, true);
        var line = Field("shippingLine", 20);
        if (type == "AUTO" && line.Length > 0)
            throw new IntakeException(422, "INVALID_REQUEST", "Invalid AUTO route", "AUTO cannot specify a shipping line.");
        var client = Field("clientRequestId", 100, true);
        var flag = Field("reprocess", 5);
        if (flag.Length > 0 && !bool.TryParse(flag, out _)) throw IntakeException.Invalid();
        return new(path, digest, size, fileName, type, line.Length == 0 ? null : line, client, flag.Length > 0 && bool.Parse(flag), "");
    }
}
