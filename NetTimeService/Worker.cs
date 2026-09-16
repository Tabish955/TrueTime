using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTimeService.Models;

namespace NetTimeService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITimeSyncCoordinator _coordinator;
    private TimeSpan _pollInterval;
    private CancellationTokenSource? _delayCts;

    public const string EventSourceName = "TrueTimeService";
    public const string EventLogName = "Application";
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(15);
    public const double OffsetThresholdMs = 500.0;

    public Worker(ILogger<Worker> logger, ITimeSyncCoordinator coordinator, IConfiguration? configuration = null)
    {
        _logger = logger;
        _coordinator = coordinator;

        int pollMinutes = configuration?.GetValue<int?>("NtpSettings:PollIntervalMinutes") ?? 15;
        _pollInterval = TimeSpan.FromMinutes(pollMinutes > 0 ? pollMinutes : 15);

        _coordinator.PollIntervalChanged += OnPollIntervalChanged;
    }

    private void OnPollIntervalChanged(TimeSpan newInterval)
    {
        _logger.LogInformation("Poll interval dynamically updated to {Minutes} minutes.", newInterval.TotalMinutes);
        _pollInterval = newInterval;
        // Wake up delay so new interval takes effect
        try
        {
            _delayCts?.Cancel();
        }
        catch { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TrueTimeService starting. Configured poll interval: {Interval} minutes, drift threshold: {Threshold}ms.",
            _pollInterval.TotalMinutes, OffsetThresholdMs);

        // Perform immediate sync on startup
        try
        {
            await _coordinator.TriggerManualSyncAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial synchronization failed: {Message}", ex.Message);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var snap = _coordinator.GetCurrentSnapshot();
            TimeSpan delay = _pollInterval;
            if (snap != null && (!snap.NetworkOnline || (!snap.InSync && string.IsNullOrEmpty(snap.SelectedServer))))
            {
                int retrySec = snap.RetryIntervalSeconds > 0 ? snap.RetryIntervalSeconds : 60;
                delay = TimeSpan.FromSeconds(retrySec);
                _logger.LogInformation("No internet connection or all servers unreachable. Scheduling retry in {Seconds}s.", retrySec);
            }

            using (_delayCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                try
                {
                    await Task.Delay(delay, _delayCts.Token);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Interval was changed dynamically; loop continues with new interval
                }
            }

            try
            {
                await _coordinator.TriggerManualSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled synchronization encountered an error: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("TrueTimeService worker stopping.");
    }

    public async Task SynchronizeTimeAsync(CancellationToken cancellationToken)
    {
        await _coordinator.TriggerManualSyncAsync(cancellationToken);
    }
}
