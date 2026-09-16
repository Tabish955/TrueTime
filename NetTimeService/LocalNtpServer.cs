using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetTimeService;

public class LocalNtpServer : IDisposable
{
    private Socket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public int Port { get; private set; } = 123;
    public DateTime LastReferenceTime { get; set; } = DateTime.UtcNow;

    public bool Start(int port = 123)
    {
        if (IsRunning) return true;
        Port = port;

        try
        {
            _cts = new CancellationTokenSource();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Bind(new IPEndPoint(IPAddress.Any, Port));

            IsRunning = true;
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        IsRunning = false;
        try
        {
            _cts?.Cancel();
            _socket?.Close();
        }
        catch { }
        finally
        {
            _socket = null;
            _cts = null;
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[1024];
        EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);

        while (!ct.IsCancellationRequested && _socket != null)
        {
            try
            {
                var result = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEp);
                if (result.ReceivedBytes >= 48)
                {
                    byte[] reply = BuildReplyPacket(buffer, LastReferenceTime);
                    await _socket.SendToAsync(new ArraySegment<byte>(reply), SocketFlags.None, result.RemoteEndPoint);
                }
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Continue listening
            }
        }
    }

    private static byte[] BuildReplyPacket(byte[] request, DateTime refTime)
    {
        byte[] packet = new byte[48];
        DateTime now = DateTime.UtcNow;

        // Byte 0: LI=0 (no warning), VN=4 (NTP v4), Mode=4 (server reply) -> 00 100 100 = 0x24
        packet[0] = 0x24;
        // Byte 1: Stratum 2 (secondary reference, synchronized by TrueTime)
        packet[1] = 2;
        // Byte 2: Poll interval (copied from client or default 6 = 64 sec)
        packet[2] = request[2] != 0 ? request[2] : (byte)6;
        // Byte 3: Precision (-20 approx 1 microsecond)
        packet[3] = 0xEC;

        // Bytes 4-7: Root delay
        packet[4] = 0; packet[5] = 0; packet[6] = 0; packet[7] = 16;
        // Bytes 8-11: Root dispersion
        packet[8] = 0; packet[9] = 0; packet[10] = 0; packet[11] = 32;

        // Bytes 12-15: Reference ID ("TRUE")
        packet[12] = (byte)'T'; packet[13] = (byte)'R'; packet[14] = (byte)'U'; packet[15] = (byte)'E';

        // Bytes 16-23: Reference Timestamp
        WriteNtpTimestamp(packet, 16, refTime);

        // Bytes 24-31: Originate Timestamp (copied from client's Transmit Timestamp at bytes 40-47)
        Array.Copy(request, 40, packet, 24, 8);

        // Bytes 32-39: Receive Timestamp
        WriteNtpTimestamp(packet, 32, now);

        // Bytes 40-47: Transmit Timestamp
        WriteNtpTimestamp(packet, 40, DateTime.UtcNow);

        return packet;
    }

    private static void WriteNtpTimestamp(byte[] buffer, int offset, DateTime time)
    {
        DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan span = time - ntpEpoch;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        ulong totalSeconds = (ulong)span.TotalSeconds;
        double fractionalSeconds = span.TotalSeconds - totalSeconds;
        ulong fractional = (ulong)(fractionalSeconds * 0x100000000L);

        buffer[offset] = (byte)((totalSeconds >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((totalSeconds >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((totalSeconds >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(totalSeconds & 0xFF);

        buffer[offset + 4] = (byte)((fractional >> 24) & 0xFF);
        buffer[offset + 5] = (byte)((fractional >> 16) & 0xFF);
        buffer[offset + 6] = (byte)((fractional >> 8) & 0xFF);
        buffer[offset + 7] = (byte)(fractional & 0xFF);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}
