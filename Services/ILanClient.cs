using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LanCopy.Models;

namespace LanCopy.Services;

/// <summary>
/// Interface for LAN file transfer client (upload/download to remote peers).
/// </summary>
public interface ILanClient : IAsyncDisposable
{
    /// <summary>
    /// Gets the remote server's IP address.
    /// </summary>
    string RemoteIp { get; }

    /// <summary>
    /// Gets the remote server's port.
    /// </summary>
    int RemotePort { get; }

    /// <summary>
    /// Lists files and directories at the specified remote path.
    /// </summary>
    Task<IReadOnlyList<FileEntry>> ListAsync(string remotePath, CancellationToken ct);

    /// <summary>
    /// Downloads a file from the remote server.
    /// </summary>
    /// <param name="remoteFile">Remote file path relative to share root.</param>
    /// <param name="localPath">Local destination path.</param>
    /// <param name="progress">Progress callback: (bytes done, total bytes).</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadAsync(string remoteFile, string localPath, IProgress<(long, long)>? progress, CancellationToken ct);

    /// <summary>
    /// Uploads a file to the remote server.
    /// </summary>
    /// <param name="localFile">Local file path to upload.</param>
    /// <param name="remoteDir">Remote directory (relative to share root).</param>
    /// <param name="progress">Progress callback: (bytes done, total bytes).</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadAsync(string localFile, string remoteDir, IProgress<(long, long)>? progress, CancellationToken ct);

    /// <summary>
    /// Authenticates with the remote server using PIN.
    /// </summary>
    Task<bool> AuthenticateAsync(string pin, CancellationToken ct);

    /// <summary>
    /// Pings the remote server to verify connection is alive.
    /// </summary>
    Task<bool> PingAsync(CancellationToken ct);

    /// <summary>
    /// Verifies a single file exists and gets its properties.
    /// </summary>
    Task<FileEntry?> GetFileInfoAsync(string remoteFile, CancellationToken ct);

    /// <summary>
    /// Deletes a file on the remote server.
    /// </summary>
    Task DeleteAsync(string remoteFile, CancellationToken ct);

    /// <summary>
    /// Renames a file on the remote server.
    /// </summary>
    Task RenameAsync(string remoteFile, string newName, CancellationToken ct);
}
