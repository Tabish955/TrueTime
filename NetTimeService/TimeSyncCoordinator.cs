using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTimeService.Models;

namespace NetTimeService;

public class TimeSyncCoordinator : ITimeSyncCoordinator
{
    private readonly ILogger<TimeSyncCoordinator> _logger;
    private readonly SntpClient _sntpClient;
    private readonly LocalNtpServer _localNtpServer = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly List<SyncHistoryEntry> _history = new();

    public const string EventSourceName = "TrueTimeService";
    public const string EventLogName = "Application";

    private List<ServerEntry> _serverEntries = new();
    private int _pollIntervalMinutes = 15;
    private double _thresholdMs = 500.0;
    private bool _enableLocalNtpServer = false;
    private int _localNtpPort = 123;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private DateTime _lastAttemptTime = DateTime.MinValue;

    public event Action<TimeSpan>? PollIntervalChanged;

    private TimeSyncSnapshot _latestSnapshot = new()
    {
        StatusMessage = "Waiting for initial synchronization."
    };

    public TimeSyncCoordinator(ILogger<TimeSyncCoordinator> logger, IConfiguration configuration, SntpClient? sntpClient = null)
    {
        _logger = logger;
        _sntpClient = sntpClient ?? new SntpClient();

        try
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }
        catch { }

        LoadConfiguration(configuration);
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _logger.LogInformation("Network availability state changed: IsAvailable={IsAvailable}", e.IsAvailable);
        if (!e.IsAvailable)
        {
            lock (_latestSnapshot)
            {
                _latestSnapshot.NetworkOnline = false;
                _latestSnapshot.InSync = false;
                _latestSnapshot.SelectedServer = null;
                _latestSnapshot.StatusMessage = "No network connection detected.";
                _latestSnapshot.RetryIntervalSeconds = 60;
                _latestSnapshot.NextSyncTime = DateTime.Now.AddSeconds(60);
                _latestSnapshot.LastAttemptTime = DateTime.Now;
            }
        }
        else
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500);
                    await TriggerManualSyncAsync();
                }
                catch { }
            });
        }
    }

    public static string GetSharedConfigPath()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string dir = Path.Combine(programData, "TrueTime");
        if (!Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
        return Path.Combine(dir, "settings.json");
    }

    private void LoadConfiguration(IConfiguration configuration)
    {
        string sharedPath = GetSharedConfigPath();
        if (File.Exists(sharedPath))
        {
            try
            {
                string json = File.ReadAllText(sharedPath);
                var savedConfig = JsonSerializer.Deserialize<AppConfigPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (savedConfig != null && savedConfig.Servers != null && savedConfig.Servers.Count > 0)
                {
                    _serverEntries = savedConfig.Servers.Select(s => new ServerEntry(s.Hostname.Trim(), s.Enabled)).ToList();
                    _pollIntervalMinutes = savedConfig.PollIntervalMinutes > 0 ? savedConfig.PollIntervalMinutes : 15;
                    _thresholdMs = savedConfig.ThresholdMilliseconds > 0 ? savedConfig.ThresholdMilliseconds : 500.0;
                    _enableLocalNtpServer = savedConfig.EnableLocalNtpServer;
                    _localNtpPort = savedConfig.LocalNtpPort > 0 ? savedConfig.LocalNtpPort : 123;

                    if (_enableLocalNtpServer)
                    {
                        _localNtpServer.Start(_localNtpPort);
                    }

                    _logger.LogInformation("Loaded custom configuration from shared store: {Path} ({Count} servers, {Interval}m poll, LAN NTP: {Lan})",
                        sharedPath, _serverEntries.Count, _pollIntervalMinutes, _enableLocalNtpServer);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to read shared settings from {Path}: {Message}", sharedPath, ex.Message);
            }
        }

        var entries = configuration.GetSection("NtpSettings:ServerEntries").Get<List<ServerEntry>>();
        if (entries != null && entries.Count > 0)
        {
            _serverEntries = entries;
        }
        else
        {
            var configServers = configuration.GetSection("NtpSettings:Servers").Get<List<string>>();
            if (configServers != null && configServers.Count > 0)
            {
                _serverEntries = configServers.Select(s => new ServerEntry(s, true)).ToList();
            }
            else
            {
                _serverEntries = SntpClient.DefaultNtpServers.Select(s => new ServerEntry(s, true)).ToList();
            }
        }

        _pollIntervalMinutes = configuration.GetValue<int?>("NtpSettings:PollIntervalMinutes") ?? 15;
        if (_pollIntervalMinutes <= 0) _pollIntervalMinutes = 15;

        _thresholdMs = configuration.GetValue<double?>("NtpSettings:ThresholdMilliseconds") ?? 500.0;
        if (_thresholdMs <= 0) _thresholdMs = 500.0;

        _enableLocalNtpServer = configuration.GetValue<bool?>("NtpSettings:EnableLocalNtpServer") ?? false;
        _localNtpPort = configuration.GetValue<int?>("NtpSettings:LocalNtpPort") ?? 123;

        if (_enableLocalNtpServer)
        {
            _localNtpServer.Start(_localNtpPort);
        }

        SaveConfigToFile();
    }

    public TimeSyncSnapshot GetCurrentSnapshot()
    {
        lock (_latestSnapshot)
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime estimatedUtc = nowUtc.AddMilliseconds(_latestSnapshot.OffsetMs);

            List<SyncHistoryEntry> historyCopy;
            lock (_history)
            {
                historyCopy = _history.Take(25).Select(h => new SyncHistoryEntry
                {
                    Timestamp = h.Timestamp,
                    Server = h.Server,
                    OffsetMs = h.OffsetMs,
                    RoundTripMs = h.RoundTripMs,
                    Stratum = h.Stratum,
                    Success = h.Success,
                    Summary = h.Summary
                }).ToList();
            }

            return new TimeSyncSnapshot
            {
                TimestampUtc = _latestSnapshot.TimestampUtc,
                LocalTime = DateTime.Now,
                EstimatedInternetTimeUtc = estimatedUtc,
                LastSyncTime = _lastSyncTime,
                OffsetMs = _latestSnapshot.OffsetMs,
                SelectedServer = _latestSnapshot.SelectedServer,
                InSync = _latestSnapshot.InSync,
                IsSyncing = _latestSnapshot.IsSyncing,
                StatusMessage = _latestSnapshot.StatusMessage,
                PollIntervalMinutes = _pollIntervalMinutes,
                ThresholdMilliseconds = _thresholdMs,
                CrystalPpm = _latestSnapshot.CrystalPpm,
                PoolJitterMs = _latestSnapshot.PoolJitterMs,
                LocalNtpServerRunning = _localNtpServer.IsRunning,
                NetworkOnline = _latestSnapshot.NetworkOnline,
                RetryIntervalSeconds = _latestSnapshot.RetryIntervalSeconds > 0 ? _latestSnapshot.RetryIntervalSeconds : 60,
                LastAttemptTime = _lastAttemptTime,
                NextSyncTime = _latestSnapshot.NextSyncTime != DateTime.MinValue
                    ? _latestSnapshot.NextSyncTime
                    : (_latestSnapshot.NetworkOnline
                        ? (_lastSyncTime != DateTime.MinValue ? _lastSyncTime.AddMinutes(_pollIntervalMinutes) : DateTime.Now.AddMinutes(_pollIntervalMinutes))
                        : DateTime.Now.AddSeconds(60)),
                ConfiguredServers = _serverEntries.Select(s => new ServerEntry(s.Hostname, s.Enabled)).ToList(),
                Servers = _latestSnapshot.Servers.Select(s => new ServerSyncDetail
                {
                    Server = s.Server,
                    RoundTripMs = s.RoundTripMs,
                    OffsetMs = s.OffsetMs,
                    Success = s.Success,
                    Error = s.Error,
                    Enabled = s.Enabled,
                    Stratum = s.Stratum,
                    JitterMs = s.JitterMs
                }).ToList(),
                History = historyCopy
            };
        }
    }

    public async Task<ServerSyncDetail> TestSingleServerAsync(string server, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing ad-hoc SNTP query to server: {Server}", server);
        var result = await _sntpClient.QueryServerAsync(server, cancellationToken);
        return new ServerSyncDetail
        {
            Server = result.Server,
            RoundTripMs = Math.Round(result.RoundTripDelay.TotalMilliseconds, 2),
            OffsetMs = Math.Round(result.Offset.TotalMilliseconds, 2),
            Success = result.Success,
            Error = result.Error,
            Enabled = true,
            Stratum = result.Stratum
        };
    }

    public async Task<List<ServerSyncDetail>> BenchmarkServersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing high-speed concurrent server benchmark");
        List<string> hosts;
        lock (_serverEntries)
        {
            hosts = _serverEntries.Where(s => s.Enabled).Select(s => s.Hostname).ToList();
        }

        if (hosts.Count == 0)
        {
            hosts = SntpClient.DefaultNtpServers.ToList();
        }

        var tasks = hosts.Select(h => TestSingleServerAsync(h, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        return results
            .OrderByDescending(r => r.Success)
            .ThenBy(r => r.RoundTripMs)
            .ToList();
    }

    public async Task<TimeSyncSnapshot> UpdateConfigAsync(AppConfigPayload newConfig, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying updated configuration via IPC: {Count} servers, {Interval}m poll, {Threshold}ms threshold, LAN NTP: {Lan}",
            newConfig.Servers.Count, newConfig.PollIntervalMinutes, newConfig.ThresholdMilliseconds, newConfig.EnableLocalNtpServer);

        lock (_serverEntries)
        {
            if (newConfig.Servers != null && newConfig.Servers.Count > 0)
            {
                _serverEntries = newConfig.Servers.Select(s => new ServerEntry(s.Hostname.Trim(), s.Enabled)).ToList();
            }

            if (newConfig.PollIntervalMinutes > 0)
            {
                _pollIntervalMinutes = newConfig.PollIntervalMinutes;
            }

            if (newConfig.ThresholdMilliseconds > 0)
            {
                _thresholdMs = newConfig.ThresholdMilliseconds;
            }

            _enableLocalNtpServer = newConfig.EnableLocalNtpServer;
            _localNtpPort = newConfig.LocalNtpPort > 0 ? newConfig.LocalNtpPort : 123;
        }

        if (_enableLocalNtpServer)
        {
            _localNtpServer.Start(_localNtpPort);
        }
        else
        {
            _localNtpServer.Stop();
        }

        PollIntervalChanged?.Invoke(TimeSpan.FromMinutes(_pollIntervalMinutes));
        SaveConfigToFile();

        return await TriggerManualSyncAsync(cancellationToken);
    }

    private void SaveConfigToFile()
    {
        try
        {
            string sharedPath = GetSharedConfigPath();
            var payload = new AppConfigPayload
            {
                Servers = _serverEntries.Select(s => new ServerEntry(s.Hostname, s.Enabled)).ToList(),
                PollIntervalMinutes = _pollIntervalMinutes,
                ThresholdMilliseconds = _thresholdMs,
                EnableLocalNtpServer = _enableLocalNtpServer,
                LocalNtpPort = _localNtpPort
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(payload, options);
            File.WriteAllText(sharedPath, json);
            _logger.LogInformation("Configuration successfully persisted to shared store: {Path}", sharedPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not persist configuration to shared store: {Message}", ex.Message);
        }
    }

    public async Task<TimeSyncSnapshot> TriggerManualSyncAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            lock (_latestSnapshot)
            {
                _latestSnapshot.IsSyncing = true;
                _latestSnapshot.StatusMessage = "Synchronizing with authoritative NTP pool...";
            }

            List<ServerEntry> currentPool;
            lock (_serverEntries)
            {
                currentPool = _serverEntries.ToList();
            }

            var activeServers = currentPool.Where(s => s.Enabled).Select(s => s.Hostname).ToList();

            if (activeServers.Count == 0)
            {
                string noServerMsg = "No enabled NTP servers configured in pool.";
                _logger.LogWarning("{Message}", noServerMsg);
                lock (_latestSnapshot)
                {
                    _latestSnapshot.IsSyncing = false;
                    _latestSnapshot.InSync = false;
                    _latestSnapshot.StatusMessage = noServerMsg;
                }
                return GetCurrentSnapshot();
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                string noNetMsg = "No local network adapter connection detected.";
                _logger.LogWarning("{Error}", noNetMsg);
                _lastAttemptTime = DateTime.Now;

                TimeSyncSnapshot noNetSnap = new TimeSyncSnapshot
                {
                    TimestampUtc = DateTime.UtcNow,
                    LocalTime = DateTime.Now,
                    LastSyncTime = _lastSyncTime,
                    LastAttemptTime = _lastAttemptTime,
                    NextSyncTime = _lastAttemptTime.AddSeconds(60),
                    Servers = currentPool.Select(s => new ServerSyncDetail
                    {
                        Server = s.Hostname,
                        RoundTripMs = 0,
                        OffsetMs = 0,
                        Success = false,
                        Error = "No network adapter connection",
                        Enabled = s.Enabled
                    }).ToList(),
                    ConfiguredServers = currentPool.Select(s => new ServerEntry(s.Hostname, s.Enabled)).ToList(),
                    PollIntervalMinutes = _pollIntervalMinutes,
                    ThresholdMilliseconds = _thresholdMs,
                    PoolJitterMs = 0,
                    LocalNtpServerRunning = _localNtpServer.IsRunning,
                    IsSyncing = false,
                    InSync = false,
                    NetworkOnline = false,
                    RetryIntervalSeconds = 60,
                    OffsetMs = 0,
                    SelectedServer = null,
                    StatusMessage = noNetMsg,
                    EstimatedInternetTimeUtc = DateTime.UtcNow
                };

                lock (_latestSnapshot)
                {
                    _latestSnapshot = noNetSnap;
                }
                return GetCurrentSnapshot();
            }

            _logger.LogInformation("Querying {Count} enabled NTP servers concurrently over UDP 123...", activeServers.Count);
            var queryResults = await _sntpClient.QueryAllServersAsync(activeServers, cancellationToken);

            var serverDetails = new List<ServerSyncDetail>();
            foreach (var entry in currentPool)
            {
                var queryResult = queryResults.FirstOrDefault(r => r.Server.Equals(entry.Hostname, StringComparison.OrdinalIgnoreCase));
                if (queryResult != null)
                {
                    serverDetails.Add(new ServerSyncDetail
                    {
                        Server = entry.Hostname,
                        RoundTripMs = Math.Round(queryResult.RoundTripDelay.TotalMilliseconds, 2),
                        OffsetMs = Math.Round(queryResult.Offset.TotalMilliseconds, 2),
                        Success = queryResult.Success,
                        Error = queryResult.Error,
                        Enabled = entry.Enabled,
                        Stratum = queryResult.Stratum
                    });
                }
                else
                {
                    serverDetails.Add(new ServerSyncDetail
                    {
                        Server = entry.Hostname,
                        RoundTripMs = 0,
                        OffsetMs = 0,
                        Success = false,
                        Error = entry.Enabled ? "Not queried" : "Disabled",
                        Enabled = entry.Enabled,
                        Stratum = 0
                    });
                }
            }

            var successful = queryResults
                .Where(r => r.Success)
                .OrderBy(r => r.RoundTripDelay)
                .ToList();

            DateTime nowUtc = DateTime.UtcNow;
            DateTime previousSync = _lastSyncTime;
            DateTime attemptTime = DateTime.Now;
            _lastAttemptTime = attemptTime;

            // Calculate Pool Jitter across all responding servers
            double poolJitter = 0;
            if (successful.Count > 1)
            {
                double avgOffset = successful.Average(r => r.Offset.TotalMilliseconds);
                double sumSq = successful.Sum(r => Math.Pow(r.Offset.TotalMilliseconds - avgOffset, 2));
                poolJitter = Math.Round(Math.Sqrt(sumSq / successful.Count), 2);
            }

            TimeSyncSnapshot newSnapshot = new TimeSyncSnapshot
            {
                TimestampUtc = nowUtc,
                LocalTime = DateTime.Now,
                LastSyncTime = _lastSyncTime,
                LastAttemptTime = attemptTime,
                Servers = serverDetails,
                ConfiguredServers = currentPool.Select(s => new ServerEntry(s.Hostname, s.Enabled)).ToList(),
                PollIntervalMinutes = _pollIntervalMinutes,
                ThresholdMilliseconds = _thresholdMs,
                PoolJitterMs = poolJitter,
                LocalNtpServerRunning = _localNtpServer.IsRunning,
                IsSyncing = false
            };

            if (successful.Count == 0)
            {
                string errMsg = "No Internet connection. Unable to reach authoritative NTP servers.";
                _logger.LogWarning("{Error}", errMsg);
                WriteEventLog(errMsg, EventLogEntryType.Warning);

                newSnapshot.InSync = false;
                newSnapshot.NetworkOnline = false;
                newSnapshot.RetryIntervalSeconds = 60;
                newSnapshot.LastAttemptTime = attemptTime;
                newSnapshot.NextSyncTime = attemptTime.AddSeconds(60);
                newSnapshot.LastSyncTime = _lastSyncTime;
                newSnapshot.OffsetMs = 0;
                newSnapshot.SelectedServer = null;
                newSnapshot.StatusMessage = errMsg;
                newSnapshot.EstimatedInternetTimeUtc = nowUtc;

                lock (_history)
                {
                    _history.Insert(0, new SyncHistoryEntry
                    {
                        Timestamp = DateTime.Now,
                        Server = "None",
                        OffsetMs = 0,
                        RoundTripMs = 0,
                        Success = false,
                        Summary = "Connection timeout across all configured servers."
                    });
                    if (_history.Count > 50) _history.RemoveAt(_history.Count - 1);
                }

                lock (_latestSnapshot)
                {
                    _latestSnapshot = newSnapshot;
                }

                return GetCurrentSnapshot();
            }

            var fastest = successful[0];
            var offset = fastest.Offset;
            var roundTrip = fastest.RoundTripDelay;
            string selectedServer = fastest.Server;

            _lastSyncTime = attemptTime;

            // Calculate Motherboard Hardware RTC Crystal Oscillator Drift in PPM (Parts Per Million)
            double ppm = 0;
            if (previousSync != DateTime.MinValue && (DateTime.Now - previousSync).TotalSeconds >= 10)
            {
                double elapsedSec = (DateTime.Now - previousSync).TotalSeconds;
                ppm = Math.Round((offset.TotalMilliseconds / (elapsedSec * 1000.0)) * 1_000_000.0, 2);
            }

            newSnapshot.SelectedServer = selectedServer;
            newSnapshot.OffsetMs = Math.Round(offset.TotalMilliseconds, 2);
            newSnapshot.EstimatedInternetTimeUtc = nowUtc + offset;
            newSnapshot.CrystalPpm = ppm;
            newSnapshot.NetworkOnline = true;
            newSnapshot.RetryIntervalSeconds = _pollIntervalMinutes * 60;
            newSnapshot.LastSyncTime = _lastSyncTime;
            newSnapshot.LastAttemptTime = attemptTime;
            newSnapshot.NextSyncTime = attemptTime.AddMinutes(_pollIntervalMinutes);

            _logger.LogInformation("Selected fastest server {Server} (Stratum {Stratum}, Latency: {Latency:F2}ms, Offset: {Offset:F2}ms, PPM: {PPM:F1}).",
                selectedServer, fastest.Stratum, roundTrip.TotalMilliseconds, offset.TotalMilliseconds, ppm);

            // Update LAN NTP reference timestamp
            _localNtpServer.LastReferenceTime = nowUtc + offset;

            if (Math.Abs(offset.TotalMilliseconds) > _thresholdMs)
            {
                DateTime targetUtc = nowUtc + offset;
                _logger.LogInformation("Offset {Offset:F2}ms exceeds threshold of {Threshold}ms. Adjusting system clock to {Target:u}...",
                    offset.TotalMilliseconds, _thresholdMs, targetUtc);

                bool adjusted = SystemClock.SetSystemTime(targetUtc, _logger);
                if (adjusted)
                {
                    string msg = $"System clock adjusted by {offset.TotalMilliseconds:F2}ms using {selectedServer}. New UTC: {targetUtc:u}.";
                    _logger.LogInformation("{Message}", msg);
                    WriteEventLog(msg, EventLogEntryType.Information);

                    newSnapshot.InSync = true;
                    newSnapshot.StatusMessage = $"Clock adjusted by {offset.TotalMilliseconds:F2}ms via {selectedServer}.";
                    newSnapshot.OffsetMs = 0;
                }
                else
                {
                    int errCode = Marshal.GetLastWin32Error();
                    string failMsg = $"Failed to adjust system clock (Win32 Error: {errCode}). Ensure SE_SYSTEMTIME_NAME privilege.";
                    _logger.LogError("{Error}", failMsg);
                    WriteEventLog(failMsg, EventLogEntryType.Error);

                    newSnapshot.InSync = false;
                    newSnapshot.StatusMessage = failMsg;
                }
            }
            else
            {
                string okMsg = $"System clock in sync (Offset: {offset.TotalMilliseconds:F2}ms via {selectedServer}, Threshold: {_thresholdMs}ms).";
                _logger.LogInformation("{Message}", okMsg);
                WriteEventLog(okMsg, EventLogEntryType.Information);

                newSnapshot.InSync = true;
                newSnapshot.StatusMessage = okMsg;
            }

            // Append to rolling synchronization history
            lock (_history)
            {
                _history.Insert(0, new SyncHistoryEntry
                {
                    Timestamp = DateTime.Now,
                    Server = selectedServer,
                    OffsetMs = Math.Round(offset.TotalMilliseconds, 2),
                    RoundTripMs = Math.Round(roundTrip.TotalMilliseconds, 2),
                    Stratum = fastest.Stratum,
                    Success = true,
                    Summary = $"Accurate ({offset.TotalMilliseconds:+0.0;-0.0;0.0} ms, Jitter: {poolJitter:F1}ms, PPM: {ppm:+0.0;-0.0;0.0})"
                });
                if (_history.Count > 50) _history.RemoveAt(_history.Count - 1);
            }

            lock (_latestSnapshot)
            {
                _latestSnapshot = newSnapshot;
            }

            return GetCurrentSnapshot();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void WriteEventLog(string message, EventLogEntryType entryType)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;

            if (!EventLog.SourceExists(EventSourceName))
            {
                EventLog.CreateEventSource(EventSourceName, EventLogName);
            }

            EventLog.WriteEntry(EventSourceName, message, entryType);
        }
        catch
        {
            // Suppress permissions failure when running without administrator EventLog registration rights
        }
    }
}
