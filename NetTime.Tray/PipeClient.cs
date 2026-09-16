using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetTimeService.Models;

namespace NetTime.Tray;

public class PipeClient
{
    private const string PipeName = "TrueTimePipe";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<TimeSyncSnapshot?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync("STATUS", 5000, cancellationToken);
    }

    public static async Task<TimeSyncSnapshot?> TriggerSyncAsync(CancellationToken cancellationToken = default)
    {
        return await SendCommandAsync("SYNC_NOW", 15000, cancellationToken);
    }

    public static async Task<TimeSyncSnapshot?> SaveConfigAsync(AppConfigPayload config, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        return await SendCommandAsync($"SAVE_CONFIG:{json}", 10000, cancellationToken);
    }

    public static async Task<ServerSyncDetail?> TestServerAsync(string server, CancellationToken cancellationToken = default)
    {
        string? raw = await SendRawCommandAsync($"TEST_SERVER:{server}", 6000, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonSerializer.Deserialize<ServerSyncDetail>(raw, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<TimeSyncSnapshot?> SendCommandAsync(string command, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        string? responseLine = await SendRawCommandAsync(command, timeoutMs, cancellationToken);
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TimeSyncSnapshot>(responseLine, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> SendRawCommandAsync(string command, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await client.ConnectAsync(linkedCts.Token);

                using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

                await writer.WriteLineAsync(command.AsMemory(), linkedCts.Token);
                await writer.FlushAsync(linkedCts.Token);

                return await reader.ReadLineAsync(linkedCts.Token);
            }
            catch (Exception) when (attempt < 2 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(250, cancellationToken);
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
