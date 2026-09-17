using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTimeService.Models;

namespace NetTimeService;

public class NamedPipeServer : BackgroundService
{
    public const string PipeName = "TrueTimePipe";
    private readonly ILogger<NamedPipeServer> _logger;
    private readonly ITimeSyncCoordinator _coordinator;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public NamedPipeServer(ILogger<NamedPipeServer> logger, ITimeSyncCoordinator coordinator)
    {
        _logger = logger;
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Named Pipe server starting on \\\\.\\pipe\\{PipeName}...", PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipeServer = CreateServerStream();
                await pipeServer.WaitForConnectionAsync(stoppingToken);

                // Dispatch client communication to a background task so the listener
                // immediately creates the next stream and accepts new connections without blocking.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(pipeServer, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error communicating with named pipe client: {Message}", ex.Message);
                    }
                    finally
                    {
                        try
                        {
                            if (pipeServer.IsConnected)
                            {
                                pipeServer.Disconnect();
                            }
                        }
                        catch
                        {
                            // Suppress disconnect errors during cleanup
                        }
                        pipeServer.Dispose();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Named Pipe error encountered; waiting 500ms before retrying...");
                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Named Pipe server stopped.");
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(pipeServer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipeServer, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        string? line = await reader.ReadLineAsync(stoppingToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string raw = line.Trim();
        string commandUpper = raw.ToUpperInvariant();

        if (commandUpper == "SYNC" || commandUpper == "SYNC_NOW")
        {
            _logger.LogInformation("Received manual SYNC request from named pipe client.");
            var response = await _coordinator.TriggerManualSyncAsync(stoppingToken);
            string json = JsonSerializer.Serialize(response, JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), stoppingToken);
        }
        else if (raw.StartsWith("SAVE_CONFIG:", StringComparison.OrdinalIgnoreCase) ||
                 raw.StartsWith("UPDATE_CONFIG:", StringComparison.OrdinalIgnoreCase) ||
                 raw.StartsWith("UPDATE_SERVERS:", StringComparison.OrdinalIgnoreCase))
        {
            int colonIndex = raw.IndexOf(':');
            string jsonPayload = raw.Substring(colonIndex + 1).Trim();
            _logger.LogInformation("Received SAVE_CONFIG request from named pipe client.");

            try
            {
                var newConfig = JsonSerializer.Deserialize<AppConfigPayload>(jsonPayload, JsonOptions);
                if (newConfig != null)
                {
                    var response = await _coordinator.UpdateConfigAsync(newConfig, stoppingToken);
                    string json = JsonSerializer.Serialize(response, JsonOptions);
                    await writer.WriteLineAsync(json.AsMemory(), stoppingToken);
                }
                else
                {
                    await writer.WriteLineAsync("ERROR: Invalid configuration payload".AsMemory(), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply configuration from named pipe.");
                await writer.WriteLineAsync($"ERROR: {ex.Message}".AsMemory(), stoppingToken);
            }
        }
        else if (raw.StartsWith("TEST_SERVER:", StringComparison.OrdinalIgnoreCase))
        {
            int colonIndex = raw.IndexOf(':');
            string targetServer = raw.Substring(colonIndex + 1).Trim();
            _logger.LogInformation("Received TEST_SERVER request for: {Server}", targetServer);

            var testResult = await _coordinator.TestSingleServerAsync(targetServer, stoppingToken);
            string json = JsonSerializer.Serialize(testResult, JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), stoppingToken);
        }
        else if (raw.Equals("BENCHMARK", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received BENCHMARK request from client");
            var benchmarkResults = await _coordinator.BenchmarkServersAsync(stoppingToken);
            string json = JsonSerializer.Serialize(benchmarkResults, JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), stoppingToken);
        }
        else
        {
            // "STATUS" or default fallback
            var response = _coordinator.GetCurrentSnapshot();
            string json = JsonSerializer.Serialize(response, JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), stoppingToken);
        }

        await writer.FlushAsync(stoppingToken);
    }

    private static NamedPipeServerStream CreateServerStream()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var pipeSecurity = new PipeSecurity();

                var currentIdentity = WindowsIdentity.GetCurrent();
                if (currentIdentity.User != null)
                {
                    pipeSecurity.AddAccessRule(new PipeAccessRule(
                        currentIdentity.User,
                        PipeAccessRights.FullControl,
                        AccessControlType.Allow));
                }

                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                return NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 4096,
                    outBufferSize: 4096,
                    pipeSecurity);
            }
            catch
            {
                // Fallback to standard creation
            }
        }

        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }
}
