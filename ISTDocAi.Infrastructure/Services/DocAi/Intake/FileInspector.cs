using ISTDocAi.Infrastructure.Configuration.DocAi;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
namespace ISTDocAi.Infrastructure.Services.DocAi.Intake;
public sealed class FileInspector(IOptions<FileInspectionOptions> options) : IFileInspector
{
    public FileInspection Inspect(string path, CancellationToken ct)
    {
        using var stream = new InspectionStream(File.OpenRead(path), TimeSpan.FromSeconds(options.Value.TimeoutSeconds), ct);
        Span<byte> header = stackalloc byte[8];
        var length = stream.Read(header);
        if (length >= 5 && header[..5].SequenceEqual("%PDF-"u8))
        {
            try
            {
                stream.Position = 0;
                using var pdf = PdfDocument.Open(stream);
                if (pdf.IsEncrypted) throw Encrypted();
                return new("application/pdf", "pdf", pdf.NumberOfPages);
            }
            catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException) { throw Encrypted(); }
            catch (IntakeException) { throw; }
            catch (Exception e) when (e is not (OutOfMemoryException or OperationCanceledException)) { throw IntakeException.Invalid(); }
        }
        if (length >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255) return new("image/jpeg", "jpg", 1);
        if (length == 8 && header.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return new("image/png", "png", 1);
        if (length >= 8 && (header[..4].SequenceEqual(new byte[] {73,73,42,0}) || header[..4].SequenceEqual(new byte[] {77,77,0,42})))
        {
            bool little = header[0] == 73;
            uint Read32(ReadOnlySpan<byte> b) => little ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b) : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);
            var offset = Read32(header[4..]);
            var seen = new HashSet<uint>();
            Span<byte> buffer = stackalloc byte[4];
            while (offset != 0)
            {
                if (!seen.Add(offset) || offset > stream.Length - 2) throw IntakeException.Invalid();
                stream.Position = offset;
                stream.ReadExactly(buffer[..2]);
                int entries = little ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer) : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(buffer);
                var next = (long)offset + 2 + entries * 12L;
                if (next > stream.Length - 4) throw IntakeException.Invalid();
                stream.Position = next;
                stream.ReadExactly(buffer);
                offset = Read32(buffer);
                if (seen.Count > short.MaxValue) throw new IntakeException(422, "TOO_MANY_PAGES", "The document has more pages than allowed", "Too many TIFF pages.");
            }
            if (seen.Count == 0) throw IntakeException.Invalid();
            return new("image/tiff", "tif", seen.Count);
        }
        throw new IntakeException(415, "UNSUPPORTED_FILE_TYPE", "Unsupported file type", "Send a PDF, JPEG, PNG or TIFF document.");
    }
    private static IntakeException Encrypted() => new(422, "ENCRYPTED_PDF", "The PDF is encrypted", "Send an unprotected PDF.");
}


