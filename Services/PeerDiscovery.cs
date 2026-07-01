using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LanCopy.Services;

// Feature 12: auto-descubrimiento UDP
// Emite broadcast cada 5s y escucha anuncios ajenos.
public sealed class PeerDiscovery : IDisposable
{
    public const int UdpPort = 8743;
    private const int MaxUdpPayload = 1024;
    private const int MaxPeerNameLen = 64;

    public record PeerInfo(string Name, string Ip, int Port, long LastSeen);

    private readonly ConcurrentDictionary<string, PeerInfo> _peers = new();
    private CancellationTokenSource? _cts;
    private readonly string _localIp;
    private readonly int _tcpPort;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private volatile HashSet<string> _localIpv4Cache;
    private bool _networkHandlerSubscribed;

    public event Action? PeersChanged;

    /// <summary>En modo sigilo no se emiten broadcasts UDP, solo se escucha.</summary>
    public bool StealthMode { get; set; }
    private readonly NetworkAddressChangedEventHandler _networkChangeHandler;

    public PeerDiscovery(string localIp, int tcpPort)
    {
        _localIp = localIp;
        _tcpPort = tcpPort;
        _localIpv4Cache = GetLocalIpv4Addresses(localIp);
        _networkChangeHandler = (_, _) =>
        {
            _localIpv4Cache = GetLocalIpv4Addresses(_localIp);
        };
    }

    public void Start()
    {
        if (_cts != null) return;
        if (!_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAddressChanged += _networkChangeHandler;
            _networkHandlerSubscribed = true;
        }
        _cts = new CancellationTokenSource();
        _ = BroadcastLoopAsync(_cts.Token);
        _ = ListenLoopAsync(_cts.Token);
        _ = ExpireLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        if (_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAddressChanged -= _networkChangeHandler;
            _networkHandlerSubscribed = false;
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public System.Collections.Generic.IReadOnlyList<PeerInfo> GetPeers()
    {
        var now = Environment.TickCount64;
        return [.. _peers.Values.Where(p => now - p.LastSeen < PeerStaleMs)];
    }

    private static string SanitizePeerName(string input)
    {
        if (string.IsNullOrEmpty(input)) return "?";
        if (input.Length > MaxPeerNameLen * 4) input = input.Substring(0, MaxPeerNameLen * 4);
        var sb = new StringBuilder(Math.Min(input.Length, MaxPeerNameLen));
        foreach (var ch in input)
        {
            var c = (int)ch;
            if (char.IsControl(ch)) continue;
            if (c == 0x202E || c == 0x202D) continue;
            if (c == 0x202A || c == 0x202B) continue;
            if (c == 0x202C) continue;
            if (c == 0x2066 || c == 0x2067 || c == 0x2068) continue;
            if (c == 0x2069) continue;
            if (c == 0x200F || c == 0x200E) continue;
            if (c == 0xFEFF) continue;
            sb.Append(ch);
            if (sb.Length >= MaxPeerNameLen) break;
        }
        return sb.Length == 0 ? "?" : sb.ToString();
    }

    internal static bool IsSameInstanceId(string? incomingInstanceId, string instanceId)
        => !string.IsNullOrWhiteSpace(incomingInstanceId)
           && string.Equals(incomingInstanceId, instanceId, StringComparison.Ordinal);

    internal static bool IsLocalIpv4Address(string ip, ISet<string> localIpv4Addresses)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetwork) return false;
        return localIpv4Addresses.Contains(address.ToString());
    }

    internal static HashSet<string> GetLocalIpv4Addresses(string? primaryLocalIp = null)
    {
        var ips = new HashSet<string>(StringComparer.Ordinal) { IPAddress.Loopback.ToString() };
        if (!string.IsNullOrWhiteSpace(primaryLocalIp)
            && IPAddress.TryParse(primaryLocalIp, out var primary)
            && primary.AddressFamily == AddressFamily.InterNetwork)
        {
            ips.Add(primary.ToString());
        }

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        ips.Add(unicast.Address.ToString());
                }
            }
        }
        catch { }

        return ips;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        var ep = new IPEndPoint(IPAddress.Broadcast, UdpPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Recalcular payload en cada ciclo para evitar datos stale tras cambios de red.
                var payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    name = SanitizePeerName(Environment.MachineName),
                    port = _tcpPort,
                    instanceId = _instanceId
                });
                // StealthMode: no emitir broadcasts, solo escuchar (invisible para otros peers)
                if (!StealthMode)
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
                if (result.Buffer.Length > MaxUdpPayload) continue;

                var doc = JsonSerializer.Deserialize<JsonElement>(result.Buffer);
                var rawName = doc.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
                var name = SanitizePeerName(rawName);
                var ip = result.RemoteEndPoint.Address.ToString();
                var port = doc.TryGetProperty("port", out var p) ? p.GetInt32() : 8742;
                var incomingInstanceId = doc.TryGetProperty("instanceId", out var iid) ? iid.GetString() : null;
                if (port < 1 || port > 65535) port = 8742;

                if (IsSameInstanceId(incomingInstanceId, _instanceId)) continue;

                if (string.IsNullOrEmpty(ip) || IsLocalIpv4Address(ip, _localIpv4Cache)) continue;

                var info = new PeerInfo(name, ip, port, Environment.TickCount64);
                if (_peers.TryGetValue(ip, out var existing) &&
                    existing.Name == info.Name && existing.Port == info.Port)
                {
                    _peers[ip] = info;
                }
                else
                {
                    _peers[ip] = info;
                    PeersChanged?.Invoke();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
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
                if (now - kv.Value.LastSeen > PeerStaleMs)
                    if (_peers.TryRemove(kv.Key, out _))
                        PeersChanged?.Invoke();
        }
    }

    public void Dispose() => Stop();

    private const long PeerStaleMs = 25_000;
}