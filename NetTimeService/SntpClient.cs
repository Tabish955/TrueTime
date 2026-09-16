using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetTimeService;

public record SntpResult(
    string Server,
    TimeSpan Offset,
    TimeSpan RoundTripDelay,
    bool Success,
    string? Error = null,
    int Stratum = 1);

public class SntpClient
{
    public static readonly string[] DefaultNtpServers =
    {
        "time.cloudflare.com",
        "time.google.com",
        "time.facebook.com",
        "time.apple.com",
        "time.windows.com",
        "pool.ntp.org",
        "time.nist.gov"
    };

    private readonly IReadOnlyList<string> _configuredServers;

    public SntpClient(IReadOnlyList<string>? servers = null)
    {
        _configuredServers = (servers != null && servers.Count > 0) ? servers : DefaultNtpServers;
    }

    public async Task<TimeSpan?> GetNetworkOffsetAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetLowestLatencyResultAsync(cancellationToken);
        return result?.Offset;
    }

    public async Task<TimeSpan?> GetLowestLatencyOffsetAsync(CancellationToken cancellationToken = default)
    {
        return await GetNetworkOffsetAsync(cancellationToken);
    }

    public async Task<SntpResult?> GetLowestLatencyResultAsync(CancellationToken cancellationToken = default)
    {
        var results = await QueryAllServersAsync(_configuredServers, cancellationToken);
        var successful = results.Where(r => r.Success).OrderBy(r => r.RoundTripDelay).ToList();
        return successful.Count > 0 ? successful[0] : null;
    }

    public async Task<IReadOnlyList<SntpResult>> QueryAllServersAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAllServersAsync(_configuredServers, cancellationToken);
    }

    public async Task<IReadOnlyList<SntpResult>> QueryAllServersAsync(IReadOnlyList<string> servers, CancellationToken cancellationToken = default)
    {
        var serverList = (servers != null && servers.Count > 0) ? servers : _configuredServers;
        var tasks = new Task<SntpResult>[serverList.Count];
        for (int i = 0; i < serverList.Count; i++)
        {
            tasks[i] = QueryServerAsync(serverList[i], cancellationToken);
        }

        return await Task.WhenAll(tasks);
    }

    public async Task<SntpResult> QueryServerAsync(string server, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            byte[] ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI=0, VN=3, Mode=3 (Client)

            var addresses = await Dns.GetHostAddressesAsync(server, linkedCts.Token);
            if (addresses.Length == 0)
            {
                return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, "DNS lookup returned no addresses");
            }

            var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
            var endPoint = new IPEndPoint(ip, 123);

            using var socket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            await socket.ConnectAsync(endPoint, linkedCts.Token);

            DateTime t0 = DateTime.UtcNow;
            await socket.SendAsync(ntpData, SocketFlags.None, linkedCts.Token);

            int bytesReceived = await socket.ReceiveAsync(ntpData, SocketFlags.None, linkedCts.Token);
            DateTime t3 = DateTime.UtcNow;

            if (bytesReceived < 48)
            {
                return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, "Truncated SNTP packet received");
            }

            byte mode = (byte)(ntpData[0] & 0x07);
            byte stratum = ntpData[1];

            if (mode != 4)
            {
                return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, $"Invalid server response mode: {mode}");
            }
            if (stratum == 0)
            {
                return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, "Kiss-o'-Death packet received (Stratum 0)");
            }

            DateTime t1 = ReadNtpTimestamp(ntpData, 32);
            DateTime t2 = ReadNtpTimestamp(ntpData, 40);

            if (t2 == DateTime.MinValue)
            {
                return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, "Invalid server transmit timestamp");
            }

            TimeSpan roundTrip;
            TimeSpan offset;

            if (t1 > DateTime.MinValue && t2 >= t1)
            {
                TimeSpan serverProcessingTime = t2 - t1;
                roundTrip = (t3 - t0) - serverProcessingTime;
                if (roundTrip < TimeSpan.Zero) roundTrip = t3 - t0;
                offset = TimeSpan.FromTicks(((t1 - t0).Ticks + (t2 - t3).Ticks) / 2);
            }
            else
            {
                roundTrip = t3 - t0;
                offset = (t2 - t0) - TimeSpan.FromTicks(roundTrip.Ticks / 2);
            }

            return new SntpResult(server, offset, roundTrip, true, null, (int)stratum);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, "Request timed out after 3 seconds");
        }
        catch (Exception ex)
        {
            return new SntpResult(server, TimeSpan.Zero, TimeSpan.Zero, false, ex.Message);
        }
    }

    private static DateTime ReadNtpTimestamp(byte[] buffer, int offset)
    {
        ulong intPart = ((ulong)buffer[offset] << 24) |
                        ((ulong)buffer[offset + 1] << 16) |
                        ((ulong)buffer[offset + 2] << 8) |
                        buffer[offset + 3];

        ulong fractPart = ((ulong)buffer[offset + 4] << 24) |
                          ((ulong)buffer[offset + 5] << 16) |
                          ((ulong)buffer[offset + 6] << 8) |
                          buffer[offset + 7];

        if (intPart == 0 && fractPart == 0)
        {
            return DateTime.MinValue;
        }

        double milliseconds = (intPart * 1000.0) + ((fractPart * 1000.0) / 0x100000000L);
        DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return ntpEpoch.AddMilliseconds(milliseconds);
    }
}