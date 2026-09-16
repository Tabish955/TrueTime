using System;
using System.Collections.Generic;

namespace NetTimeService.Models;

public class ServerEntry
{
    public string Hostname { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public ServerEntry() { }

    public ServerEntry(string hostname, bool enabled = true)
    {
        Hostname = hostname;
        Enabled = enabled;
    }
}

public class ServerSyncDetail
{
    public string Server { get; set; } = string.Empty;
    public double RoundTripMs { get; set; }
    public double OffsetMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool Enabled { get; set; } = true;
    public int Stratum { get; set; } = 1;
    public double JitterMs { get; set; }
}

public class SyncHistoryEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Server { get; set; } = string.Empty;
    public double OffsetMs { get; set; }
    public double RoundTripMs { get; set; }
    public int Stratum { get; set; } = 1;
    public bool Success { get; set; } = true;
    public string Summary { get; set; } = "OK";
}

public class TimeSyncSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime LocalTime { get; set; } = DateTime.Now;
    public DateTime EstimatedInternetTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSyncTime { get; set; } = DateTime.MinValue;
    public double OffsetMs { get; set; }
    public string? SelectedServer { get; set; }
    public bool InSync { get; set; }
    public bool IsSyncing { get; set; }
    public string StatusMessage { get; set; } = "Initializing";
    public List<ServerSyncDetail> Servers { get; set; } = new();
    public List<ServerEntry> ConfiguredServers { get; set; } = new();
    public int PollIntervalMinutes { get; set; } = 15;
    public double ThresholdMilliseconds { get; set; } = 500.0;

    // Advanced features competitors lack
    public double CrystalPpm { get; set; } // Calculated hardware crystal oscillator drift (PPM)
    public double PoolJitterMs { get; set; } // Statistical network jitter across authoritative pool
    public bool LocalNtpServerRunning { get; set; } // Local LAN broadcast server mode
    public bool NetworkOnline { get; set; } = true; // False if no internet/all NTP servers unreachable
    public int RetryIntervalSeconds { get; set; } = 60; // Seconds until next retry on failure
    public DateTime LastAttemptTime { get; set; } = DateTime.MinValue; // Last time sync was triggered/attempted
    public DateTime NextSyncTime { get; set; } = DateTime.MinValue; // Target time for next scheduled sync
    public List<SyncHistoryEntry> History { get; set; } = new();
}

public class AppConfigPayload
{
    public List<ServerEntry> Servers { get; set; } = new();
    public int PollIntervalMinutes { get; set; } = 15;
    public double ThresholdMilliseconds { get; set; } = 500.0;
    public bool EnableLocalNtpServer { get; set; } = false;
    public int LocalNtpPort { get; set; } = 123;
}

public class NtpSettings
{
    public List<string> Servers { get; set; } = new()
    {
        "time.cloudflare.com",
        "time.google.com",
        "time.facebook.com",
        "time.apple.com",
        "time.windows.com",
        "pool.ntp.org",
        "time.nist.gov"
    };
    public List<ServerEntry> ServerEntries { get; set; } = new();
    public int PollIntervalMinutes { get; set; } = 15;
    public double ThresholdMilliseconds { get; set; } = 500.0;
    public bool EnableLocalNtpServer { get; set; } = false;
    public int LocalNtpPort { get; set; } = 123;
}
