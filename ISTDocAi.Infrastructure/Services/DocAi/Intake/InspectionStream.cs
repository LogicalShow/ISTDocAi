using System.Diagnostics;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;

namespace ISTDocAi.Infrastructure.Services.DocAi.Intake;

// PdfPig reads only the page tree here. Bound its IO, including seeks during malformed-file recovery.
internal sealed class InspectionStream(Stream inner, TimeSpan timeout, CancellationToken cancellation) : Stream
{
    private readonly long started = Stopwatch.GetTimestamp();
    private void Check()
    {
        cancellation.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(started) > timeout) throw IntakeException.Invalid();
    }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get { Check(); return inner.Length; } }
    public override long Position { get { Check(); return inner.Position; } set { Check(); inner.Position = value; } }
    public override int Read(byte[] buffer, int offset, int count) { Check(); return inner.Read(buffer, offset, count); }
    public override int Read(Span<byte> buffer) { Check(); return inner.Read(buffer); }
    public override int ReadByte() { Check(); return inner.ReadByte(); }
    public override long Seek(long offset, SeekOrigin origin) { Check(); return inner.Seek(offset, origin); }
    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
