using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LanCopy.Services;

// Feature 12: auto-descubrimiento UDP
// Emite broadcast cada 5s con nombre/IP/puerto; escucha anuncios ajenos.
public sealed class PeerDiscovery : IDisposable
{
    public const int UdpPort = 8743;

    public record PeerInfo(string Name, string Ip, int Port, long LastSeen);

    private readonly ConcurrentDictionary<string, PeerInfo> _peers = new();
    private CancellationTokenSource? _cts;
    private readonly string _localIp;
    private readonly int _tcpPort;

    public event Action? PeersChanged;

    public PeerDiscovery(string localIp, int tcpPort)
    {
        _localIp = localIp;
        _tcpPort = tcpPort;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = BroadcastLoopAsync(_cts.Token);
        _ = ListenLoopAsync(_cts.Token);
        _ = ExpireLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public System.Collections.Generic.IReadOnlyList<PeerInfo> GetPeers()
    {
        var now = Environment.TickCount64;
        return [.. _peers.Values.Where(p => now - p.LastSeen < 20_000)];
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = Environment.MachineName,
            ip = _localIp,
            port = _tcpPort
        });
        var ep = new IPEndPoint(IPAddress.Broadcast, UdpPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await udp.SendAsync(payload, ep, ct);
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(5000, ct); }
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, UdpPort));
        ct.Register(() => udp.Dispose());
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                var name = doc.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
                var ip = doc.TryGetProperty("ip", out var i) ? i.GetString() ?? "" : "";
                var port = doc.TryGetProperty("port", out var p) ? p.GetInt32() : 8742;

                if (string.IsNullOrEmpty(ip) || ip == _localIp) continue; // ignorar propio

                var info = new PeerInfo(name, ip, port, Environment.TickCount64);
                _peers[ip] = info;
                PeersChanged?.Invoke();
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task ExpireLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(10_000, ct); } catch { break; }
            var now = Environment.TickCount64;
            foreach (var kv in _peers)
                if (now - kv.Value.LastSeen > 20_000)
                    if (_peers.TryRemove(kv.Key, out _))
                        PeersChanged?.Invoke();
        }
    }

    public void Dispose() => Stop();
}
