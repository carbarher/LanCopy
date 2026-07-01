using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LanCopy.Services;

/// <summary>
/// Interface for discovering LAN peers running LanCopy.
/// </summary>
public interface IPeerDiscovery : IAsyncDisposable
{
    /// <summary>
    /// Represents a discovered peer.
    /// </summary>
    public record DiscoveredPeer(
        string Ip,
        int Port,
        string HostName,
        string DeviceName,
        DateTime LastSeen,
        bool TlsEnabled);

    /// <summary>
    /// Event raised when a new peer is discovered or updated.
    /// </summary>
    event EventHandler<PeerDiscoveryEventArgs>? PeerDiscovered;

    /// <summary>
    /// Event raised when a peer disappears from the network.
    /// </summary>
    event EventHandler<PeerDiscoveryEventArgs>? PeerExpired;

    /// <summary>
    /// Starts the discovery process (broadcasts on LAN to find peers).
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stops the discovery process.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets the current list of discovered peers.
    /// </summary>
    IReadOnlyList<DiscoveredPeer> GetDiscoveredPeers();

    /// <summary>
    /// Gets a specific peer by IP address, if discovered.
    /// </summary>
    DiscoveredPeer? GetPeer(string ip);

    /// <summary>
    /// Refreshes the discovery (forces a new scan).
    /// </summary>
    Task RefreshAsync(CancellationToken ct);

    /// <summary>
    /// Gets the local IP address used for discovery.
    /// </summary>
    string GetLocalIp();
}

/// <summary>
/// Event args for peer discovery events.
/// </summary>
public class PeerDiscoveryEventArgs : EventArgs
{
    public required string Ip { get; init; }
    public required string HostName { get; init; }
    public required string DeviceName { get; init; }
    public int Port { get; init; }
    public bool TlsEnabled { get; init; }
    public required PeerDiscoveryAction Action { get; init; }
}

public enum PeerDiscoveryAction
{
    Added,
    Updated,
    Removed
}
