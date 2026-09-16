using System;
using System.Threading;
using System.Threading.Tasks;
using NetTimeService.Models;

namespace NetTimeService;

public interface ITimeSyncCoordinator
{
    TimeSyncSnapshot GetCurrentSnapshot();
    Task<TimeSyncSnapshot> TriggerManualSyncAsync(CancellationToken cancellationToken = default);
    Task<ServerSyncDetail> TestSingleServerAsync(string server, CancellationToken cancellationToken = default);
    Task<TimeSyncSnapshot> UpdateConfigAsync(AppConfigPayload newConfig, CancellationToken cancellationToken = default);
    event Action<TimeSpan>? PollIntervalChanged;
}
