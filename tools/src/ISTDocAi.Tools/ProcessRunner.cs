using System.Diagnostics;

namespace ISTDocAi.Tools;

public interface IProcessRunner
{
    Task<int> RunAsync(ProcessStartInfo start, TextWriter output);
    void Open(ProcessStartInfo start);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ProcessStartInfo start, TextWriter output)
    {
        using var process = new Process { StartInfo = start };
        process.Start();
        // Synchronize the two pumps without buffering either stream until process exit.
        var synchronized = TextWriter.Synchronized(output);
        var stdout = PumpAsync(process.StandardOutput, synchronized);
        var stderr = PumpAsync(process.StandardError, synchronized);
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync());
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, TextWriter writer)
    {
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
            await writer.WriteAsync(buffer.AsMemory(0, count));
    }

    public void Open(ProcessStartInfo start) { using var process = Process.Start(start); }
}
