using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using QRCoder;
using Avalonia.Styling;
using Avalonia.Threading;
using LanCopy.Models;
using LanCopy.Localization;
using LanCopy.Services;

namespace LanCopy;

public partial class MainWindow
{
    private readonly record struct TransferProgressSnapshot(long DoneBytes, long TotalBytes);

    private sealed class TransferProgressAggregate(long totalBytes)
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, long> _activeBytes = new();
        private long _completedBytes;

        public TransferProgressSnapshot Update(int key, long doneBytes)
        {
            lock (_sync)
            {
                _activeBytes[key] = Math.Max(0, doneBytes);
                return SnapshotLocked();
            }
        }

        public TransferProgressSnapshot Complete(int key, long fileBytes)
        {
            lock (_sync)
            {
                _activeBytes.Remove(key);
                _completedBytes = Math.Min(totalBytes, _completedBytes + Math.Max(0, fileBytes));
                return SnapshotLocked();
            }
        }

        public TransferProgressSnapshot Fail(int key)
        {
            lock (_sync)
            {
                _activeBytes.Remove(key);
                return SnapshotLocked();
            }
        }

        public TransferProgressSnapshot Snapshot()
        {
            lock (_sync) return SnapshotLocked();
        }

        private TransferProgressSnapshot SnapshotLocked()
        {
            long active = 0;
            foreach (var v in _activeBytes.Values) active += v;
            var done = Math.Min(totalBytes, _completedBytes + active);
            return new TransferProgressSnapshot(done, totalBytes);
        }
    }

    private async Task TransferAsync(
        List<FileEntry> items,
        bool isUpload,
        List<(FileEntry entry, string destPath)>? replayQueueFiles = null,
        string? targetIp = null,
        int? targetPort = null,
        HashSet<int>? precomputedSkipSet = null,
        bool silent = false)
    {
        ref int isFlag = ref (isUpload ? ref _isUploading : ref _isDownloading);
        if (Interlocked.CompareExchange(ref isFlag, 1, 0) == 1) return;

        CancellationTokenSource cts;
        if (isUpload) { _uploadCts?.Dispose(); _uploadCts = new CancellationTokenSource(); cts = _uploadCts; }
        else { _downloadCts?.Dispose(); _downloadCts = new CancellationTokenSource(); cts = _downloadCts; }
        var ct = cts.Token;

        // Capturar IP/puerto en hilo UI antes de entrar en async
        var remoteIp = targetIp ?? this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
        var portStr = targetPort?.ToString() ?? this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
        int remotePort;
        if (targetPort == null) int.TryParse(portStr, out remotePort);
        else remotePort = targetPort.Value;

        var pwTitle = isUpload ? L["prog.title.send"] : L["prog.title.receive"]; // Q1: sin prefijo __
        if (!silent)
        {
            _progressWin = new ProgressWindow(pwTitle, cts);
            _progressWin.Show(this);
            SetTransferButtonsEnabled(false, cancelEnabled: true);
        }
        else
        {
            _progressWin = null;
        }
        string finalMsg = "";
        bool finalErr = false;
        var sw = System.Diagnostics.Stopwatch.StartNew(); // Q1
        try
        {
            if (!await EnsureHealthyTransferConnectionAsync(remoteIp, remotePort, ct))
            {
                finalMsg = L["st.reconnectFailed"];
                finalErr = true;
                SetStatusAlert(finalMsg);
                return;
            }

            var fileList = replayQueueFiles ?? await ExpandItemsAsync(items, isUpload, ct);
        // Q5: ordenar por tama�o si "small first" est� activado
        if (_sortSmallestFirst && fileList != null)
            fileList = fileList.OrderBy(x => x.entry.Size).ToList();
            if (fileList == null || ct.IsCancellationRequested) return;

            // Feature 3: guardar cola persistente al iniciar
            if (replayQueueFiles == null)
                SaveQueue(fileList, isUpload, remoteIp, remotePort);

            var totalBytes = fileList.Sum(x => x.entry.Size);
            var arrow = isUpload ? ">>" : "<<";
            SetStatus(L.Format("st.filesTotal", $"{arrow} {fileList.Count}", fileList.Count > 1 ? L["word.files"] : L["word.file"], FileEntry.FormatSize(totalBytes)));

            var skipSet = precomputedSkipSet ?? await CheckOverwriteAsync(fileList, isUpload, ct: ct);
            if (skipSet == null)
            {
                finalErr = true;
                finalMsg = L["st.queueKeptRetry"];
                SetStatusAlert(finalMsg);
                return;
            }

            int ok = 0, skip = 0;
            long totalTransfer = fileList
                .Where((_, i) => !skipSet.Contains(i))
                .Sum(x => x.entry.Size);
            var aggregate = new TransferProgressAggregate(totalTransfer);
            BeginTransferUi(receiving: !isUpload, totalTransfer);

            var failedFiles = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
            var lockDoneBytes = new object();
            long doneBytes = 0; // actualizada dentro de lockDoneBytes

            static List<(FileEntry entry, string destPath)> DeduplicateFailures(IEnumerable<(FileEntry entry, string destPath)>? items)
            {
                // BUG-FIX #4: Validar items no sea null antes de GroupBy
                if (items == null) return [];
                return items
                    .GroupBy(x => $"{x.entry.FullPath}|{x.destPath}", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();
            }

            // -- Pase 1: transferencias paralelas (máx _maxParallel simultáneas) --------
            // P1: Parallel.ForEachAsync crea tasks lazily — evita N tasks en memoria para N ficheros (fix B1)
            skip = Enumerable.Range(0, fileList.Count).Count(i => skipSet.Contains(i));
            await Parallel.ForEachAsync(
                Enumerable.Range(0, fileList.Count).Where(fi => !skipSet.Contains(fi)),
                new ParallelOptions { MaxDegreeOfParallelism = _maxParallel, CancellationToken = ct },
                async (fi, innerCt) =>
                {
                    var (entry, destPath) = fileList[fi];
                    await ParallelTransferFileAsync(
                        entry, destPath, isUpload, fi, fileList.Count,
                        totalTransfer, arrow, failedFiles, lockDoneBytes, aggregate, remoteIp, remotePort, innerCt);
                });

            // Contar OK y actualizar doneBytes final
            var uniqueFailedAfterPass1 = DeduplicateFailures(failedFiles);
            lock (lockDoneBytes)
                doneBytes = totalTransfer - uniqueFailedAfterPass1.Sum(x => (long)x.entry.Size);
            ok = fileList.Count - skip - uniqueFailedAfterPass1.Count;

            // -- Pase 2+: reintentar archivos fallidos (múltiples intentos) ------
            const int maxRetryPasses = TransferOptions.MaxRetryPasses; // centralizado en TransferOptions
            int retryPass = 0;
            int recoveredOnRetries = 0;
            while (failedFiles.Count > 0 && !ct.IsCancellationRequested && retryPass < maxRetryPasses)
            {
                retryPass++;
                var pending = DeduplicateFailures(failedFiles);
                failedFiles.Clear();

                SetStatus(L.Format("st.retrying", pending.Count));
                var backoffMs = Math.Min(2000 * retryPass, 8000);
                try { await Task.Delay(backoffMs, ct); } catch (OperationCanceledException) { break; }

                bool reconnected2 = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (!reconnected2)
                {
                    foreach (var f in pending) failedFiles.Add(f);
                    break;
                }

                var retryBag = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
                // P1: retry también con Parallel.ForEachAsync — lazy task creation
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, pending.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = _maxParallel, CancellationToken = ct },
                    async (ri, innerCt) =>
                    {
                        if (innerCt.IsCancellationRequested) return;
                        var (entry, destPath) = pending[ri];
                        await ParallelTransferFileAsync(
                            entry, destPath, isUpload, ri, pending.Count,
                            totalTransfer, $"↺{arrow}", retryBag, lockDoneBytes, aggregate, remoteIp, remotePort, innerCt);
                    });

                var recoveredThisPass = pending.Count - retryBag.Count;
                ok += recoveredThisPass;
                recoveredOnRetries += recoveredThisPass;
                foreach (var f in retryBag) failedFiles.Add(f);

                var uniqueFailed = DeduplicateFailures(failedFiles);
                lock (lockDoneBytes)
                    doneBytes = totalTransfer - uniqueFailed.Sum(x => (long)x.entry.Size);
            }

            var finalUniqueFailures = DeduplicateFailures(failedFiles);
            if (finalUniqueFailures.Count > 0)
            {
                var names = string.Join(", ", finalUniqueFailures.Select(x => x.entry.Name).Take(3));
                var extra = finalUniqueFailures.Count > 3 ? $" +{finalUniqueFailures.Count - 3}" : "";
                SetStatusAlert(L.Format("hist.incomplete", finalUniqueFailures.Count, $"{names}{extra}"));
            }
            else if (retryPass > 0 && recoveredOnRetries > 0)
            {
                SetStatus(L.Format("hist.retryOk", recoveredOnRetries));
            }

            var msg = skip > 0
                ? L.Format("msg.copiedSkipped", ok, skip, fileList.Count)
                : L.Format("msg.copiedFiles", ok, fileList.Count, FileEntry.FormatSize(doneBytes));
            SetStatus(msg);
            finalMsg = msg;
            finalErr = (ok < fileList.Count - skip) || skip > 0;
            if (finalErr) SetStatusAlert(msg); // requiere lectura: parpadeo llamativo
            var finalSnapshot = aggregate.Snapshot();
            if (!ct.IsCancellationRequested && !finalErr && ok > 0)
            {
                CompleteTransferUi(receiving: !isUpload, finalSnapshot.DoneBytes, finalSnapshot.TotalBytes, sw.Elapsed);
                // Notificación cuando la ventana no es visible (minimizada a tray)
                NotifyTransferComplete(isUpload, ok, doneBytes, sw.Elapsed);
                PlayTransferSound();
            }
            else if (!ct.IsCancellationRequested)
                SuspendTransferUi();
            else
                ResetTransferUi();
            if (ok > 0) {
                AuditService.Record(remoteIp, isUpload ? "send" : "receive",
                    ok == 1 && fileList.Count == 1 ? fileList[0].entry.Name : $"[{ok} files]",
                    doneBytes, true, sw.ElapsedMilliseconds);
            }

            if (isUpload) await RefreshRemoteAsync();
            else await RefreshLocalAsync();
        }
        finally
        {
            // Resetear pausa por si cancelaron mientras pausado
            if (Interlocked.Exchange(ref _isPaused, 0) == 1)
            {
                try { _pauseSemaphore.Release(); }
                catch (SemaphoreFullException ex)
                {
                    Log.Debug("transfer", "pause-semaphore-already-released", new { error = ex.Message });
                }
            }
            if (_progressWin != null)
            {
                _progressWin.Finish(string.IsNullOrEmpty(finalMsg) ? L["prog.cancelled"] : finalMsg, finalErr);
                _progressWin = null;
            }
            ref int flagEnd = ref (isUpload ? ref _isUploading : ref _isDownloading);
            Interlocked.Exchange(ref flagEnd, 0);
            if (!silent)
            {
                SetTransferButtonsEnabled(true, cancelEnabled: false);
            }
            if (!finalErr && !ct.IsCancellationRequested)
                ClearQueue(); // completada correctamente
            else
                SetStatusAlert(L["st.queueKeptRetry"]);
        }
    }

    private async Task<bool> EnsureHealthyTransferConnectionAsync(string ip, int port, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ip) || port < 1) return false;

        LanClient? snap;
        await _clientLock.WaitAsync(ct);
        try { snap = _client; }
        finally { _clientLock.Release(); }

        if (snap != null)
        {
            try
            {
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probeCts.CancelAfter(TimeSpan.FromSeconds(3));
                _ = await snap.ListAsync(_remotePath, probeCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                // conexión inválida; intentar reconectar abajo
                Log.Debug("transfer", "connection-health-probe-failed", new { ip, port, error = ex.Message });
            }
        }

        return await TryReconnectAsync(ip, port, ct);
    }

    /// <summary>
    /// Transfiere un único archivo. Devuelve true si éxito, false si error o cancelado.
    /// </summary>
    // Progreso del lado servidor (cuando el OTRO PC inicia la copia): recepcion 'put' / envio 'get'.
    private void OnServerTransferProgress(FileServer.TransferProgressInfo info)
    {
        ReportTransferUi(info.Receiving, info.Done, info.Total);
        if (info.Total > 0 && info.Done >= info.Total)
            CompleteTransferUi(info.Receiving, info.Done, info.Total, TimeSpan.Zero);
    }
    private async Task<bool> DoTransferFileAsync(
        FileEntry entry, string destPath, bool isUpload,
        int fi, int total, long totalTransfer, TransferProgressAggregate aggregate, int progressKey,
        string remoteIp, int remotePort, CancellationToken ct,
        bool isParallel = false)
    {
        var prog = new Progress<(long done, long total)>(v =>
        {
            var snapshot = aggregate.Update(progressKey, v.done);
            ReportTransferUi(receiving: !isUpload, snapshot.DoneBytes, totalTransfer > 0 ? totalTransfer : v.total);
        });

        var throttled = new ThrottledProgress<(long, long)>(prog, intervalMs: 200);

        // Feature 2: upload usa _client/_clientLock, download usa _clientDown/_clientLockDown
        // BUG-FIX-B3: En modo paralelo (isParallel=true), cada tarea crea su propio LanClient
        // en lugar de compartir _clientDown, que no es thread-safe (causa corrupcion de protocolo).
        var clientLock = isUpload ? _clientLock : _clientLockDown;

        LanClient? snap = null;
        LanClient? ownedParallelClient = null; // cliente propio si isParallel && !isUpload
        try
        {
            if (!isUpload && isParallel)
            {
                // Modo paralelo download: cliente dedicado por tarea (LanClient no es thread-safe).
                // DownloadAsync gestiona la conexion internamente (igual que Broadcast usa MakeClient+UploadAsync).
                ownedParallelClient = MakeClient(remoteIp, remotePort);
                snap = ownedParallelClient;
            }
            else
            {
                await clientLock.WaitAsync(ct);
                try
                {
                    if (isUpload)
                    {
                        if (_client == null) return false;
                        snap = _client;
                    }
                    else
                    {
                        if (_clientDown == null) _clientDown = new LanClient(remoteIp, remotePort);
                        snap = _clientDown;
                    }
                }
                finally { clientLock.Release(); }
            }

            if (isUpload)
            {
                bool deltaSuccess = false;
                try
                {
                    // Intentar subida delta primero (se autocancela y pasa a normal si no existe el remoto)
                    deltaSuccess = await snap.UploadDeltaAsync(entry.FullPath, destPath, throttled, ct);
                }
                catch (Exception dEx)
                {
                    Log.Debug("transfer", "upload-delta-failed-fallback", new { file = entry.Name, error = dEx.Message });
                }

                if (!deltaSuccess)
                {
                    await snap.UploadAsync(
                        entry.FullPath,
                        destPath,
                        throttled,
                        ct,
                        (accepted, total) =>
                        {
                            var fromText = FileEntry.FormatSize(accepted);
                            var totalText = FileEntry.FormatSize(total);
                            SetStatus(L.Format("st.resumingFrom", fromText, totalText, entry.Name));
                        });
                }
            }
            else
            {
                bool deltaSuccess = false;
                try
                {
                    // Intentar descarga delta primero
                    deltaSuccess = await snap.DownloadDeltaAsync(entry.FullPath, destPath, throttled, ct);
                }
                catch (Exception dEx)
                {
                    Log.Debug("transfer", "download-delta-failed-fallback", new { file = entry.Name, error = dEx.Message });
                }

                if (!deltaSuccess)
                {
                    // Si el archivo es grande (> 10 MB), usar descarga paralela multi-hilo
                    if (entry.Size > 10L * 1024 * 1024)
                    {
                        await snap.DownloadParallelAsync(entry.FullPath, destPath, threads: 4, throttled, ct);
                    }
                    else
                    {
                        await snap.DownloadAsync(entry.FullPath, destPath, throttled, ct);
                    }

                    // F2: verificación de integridad post-download (SHA-256) si no fue delta (delta ya verifica)
                    if (_checksumEnabled && !ct.IsCancellationRequested && File.Exists(destPath))
                    {
                        SetStatus(L.Format("st.checksumVerifying", entry.Name));
                        var remoteHash = await snap.GetSha256Async(entry.FullPath, ct);
                        string localHash;
                        await using (var fs = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true))
                            localHash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();

                        if (remoteHash != null && !string.Equals(remoteHash, localHash, StringComparison.OrdinalIgnoreCase))
                        {
                            SetStatus(L.Format("st.checksumFail", entry.Name));
                            try { File.Delete(destPath); }
                            catch (Exception deleteEx)
                            {
                                Log.Warn("transfer", "delete-corrupt-download-failed", new { path = destPath, error = deleteEx.Message });
                            }
                            return false; // marcar para reintento
                        }
                    }
                }
            }

            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            Log.Warn("transfer", "file-transfer-failed", new { file = entry.Name, dest = destPath, isUpload, error = ex.Message });
            // Invalidar cliente para forzar reconexión. Para clientes propios (isParallel download),
            // simplemente se descartan al final del finally. Para clientes compartidos, invalidar.
            if (!isParallel)
            {
                await _clientLock.WaitAsync(CancellationToken.None);
                try { if (isUpload && _client == snap) { _client?.Dispose(); _client = null; } }
                finally { _clientLock.Release(); }
                await _clientLockDown.WaitAsync(CancellationToken.None);
                try { if (!isUpload && _clientDown == snap) { _clientDown?.Dispose(); _clientDown = null; } }
                finally { _clientLockDown.Release(); }
            }
            return false;
        }
        finally
        {
            // Liberar cliente propio de descarga paralela
            if (ownedParallelClient != null)
                try { await ownedParallelClient.DisposeAsync(); } catch { }
        }
    }

    // Feature 13: transferencia paralela con límite de 4 simultáneas
    private async Task ParallelTransferFileAsync(
        FileEntry entry, string destPath, bool isUpload,
        int fi, int total, long totalTransfer, string arrow,
        System.Collections.Concurrent.ConcurrentBag<(FileEntry, string)> failedBag,
        object lockDoneBytes,
        TransferProgressAggregate aggregate,
        string remoteIp, int remotePort, CancellationToken ct)
    {
        bool semaphoreHeld = false;
        try
        {
            // Feature 1: punto de pausa — WaitAsync bloquea solo cuando la transferencia está pausada.
            // BUG-FIX: si ct se cancela en la ventana de race entre el retorno de WaitAsync y la
            // comprobación, el semáforo quedaba adquirido sin liberarse (→ deadlock en futuros WaitAsync).
            // Fix: Release siempre en try/finally si WaitAsync completa sin OCE.
            bool pauseAcquired = false;
            try
            {
                await _pauseSemaphore.WaitAsync(ct);
                pauseAcquired = true;
            }
            finally
            {
                if (pauseAcquired) _pauseSemaphore.Release();
            }
            if (ct.IsCancellationRequested) return;


            // Limitar a máx 4 simultáneas
            await _transferSemaphore.WaitAsync(ct);
            semaphoreHeld = true;

            var progressKey = fi;

            bool fileOk = await DoTransferFileAsync(
                entry, destPath, isUpload, fi, total, totalTransfer, aggregate, progressKey, remoteIp, remotePort, ct, isParallel: true);

            if (!fileOk && !ct.IsCancellationRequested)
            {
                aggregate.Fail(progressKey);
                SetStatus(L.Format("st.errorReconnecting", entry.Name));
                bool reconnected = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (reconnected)
                    fileOk = await DoTransferFileAsync(
                        entry, destPath, isUpload, fi, total, totalTransfer, aggregate, progressKey, remoteIp, remotePort, ct, isParallel: true);
            }

            if (fileOk)
            {
                var snapshot = aggregate.Complete(progressKey, entry.Size);
                ReportTransferUi(receiving: !isUpload, snapshot.DoneBytes, snapshot.TotalBytes);
                // B4: bloque lock con doneNow eliminado — era codigo muerto (doneNow nunca se almacenaba)
            }
            else
            {
                aggregate.Fail(progressKey);
                ReportTransferUi(receiving: !isUpload, aggregate.Snapshot().DoneBytes, aggregate.Snapshot().TotalBytes);
                failedBag.Add((entry, destPath));
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelación normal del usuario/reintento: no propagar a async void (evita crash de la app).
        }
        finally
        {
            if (semaphoreHeld)
                _transferSemaphore.Release();
        }
    }

    // Throttle Report() en el hilo de background — evita flood del dispatcher (#9)
    private sealed class ThrottledProgress<T> : IProgress<T>
    {
        private readonly IProgress<T> _inner;
        private readonly long _intervalMs;
        private long _lastMs;

        public ThrottledProgress(IProgress<T> inner, int intervalMs = 200)
        {
            _inner = inner;
            _intervalMs = intervalMs;
        }

        public void Report(T value)
        {
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastMs) < _intervalMs) return;
            Interlocked.Exchange(ref _lastMs, now);
            _inner.Report(value);
        }
    }

    // SEGURIDAD: combina baseDir + segmentos confiando en que el resultado NO escape de baseDir.
    // Los nombres pueden venir de un peer remoto; bloquea path traversal (../) y rutas absolutas.
    private static bool TrySafeCombineUnder(string baseDir, out string dest, params string[] parts)
        => PathSafety.TryCombineUnder(baseDir, out dest, parts);

    private async Task<List<(FileEntry entry, string destPath)>?> ExpandItemsAsync(
        List<FileEntry> items, bool isUpload, CancellationToken ct)
    {
        var result = new List<(FileEntry, string)>();
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) return null;

            if (!item.IsDirectory)
            {
                string dest;
                if (isUpload)
                    dest = Path.Combine(_remotePath, item.Name);
                else if (!TrySafeCombineUnder(_localPath, out dest, item.Name))
                {
                    SetStatus(L.Format("st.errorListing", item.Name, L["err.invalidName"]));
                    continue;
                }
                result.Add((item, dest));
            }
            else
            {
                if (isUpload)
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(item.FullPath, "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(item.FullPath, f);
                            var dest = Path.Combine(_remotePath, item.Name, rel);
                            var fii = new FileInfo(f);
                            // Tag="dir" → saltar overwrite check (subcarpetas remotas desconocidas) (#3)
                            result.Add((new FileEntry { Name = fii.Name, FullPath = f, Size = fii.Length, Tag = "dir" }, dest));
                        }
                    }
                    catch (Exception ex) { SetStatus(L.Format("st.errorListing", item.Name, L[ex.Message])); }
                }
                else
                {
                    try
                    {
                        LanClient? snap;
                        await _clientLock.WaitAsync(ct);
                        try { snap = _client; }
                        finally { _clientLock.Release(); }
                        if (snap == null) continue;

                        var remoteFiles = await snap.ListRecursiveAsync(item.FullPath, ct);
                        foreach (var rf in remoteFiles)
                        {
                            // F6: ruta relativa desde root de carpeta para preservar estructura
                            var relPath = (!string.IsNullOrEmpty(rf.FullPath) &&
                                          rf.FullPath.Length > item.FullPath.Length &&
                                          rf.FullPath.StartsWith(item.FullPath, StringComparison.OrdinalIgnoreCase))
                                ? rf.FullPath[item.FullPath.Length..].TrimStart('/', '\\')
                                : rf.Name;
                            if (string.IsNullOrEmpty(relPath)) relPath = rf.Name;
                            if (!TrySafeCombineUnder(_localPath, out var dest, item.Name, relPath)) continue;
                            // Crear directorio intermedio para subdirectorios anidados
                            var destDir = Path.GetDirectoryName(dest);
                            if (destDir != null)
                            {
                                try { Directory.CreateDirectory(destDir); }
                                catch (Exception dirEx)
                                {
                                    Log.Debug("transfer", "create-subdirectory-failed", new { path = destDir, error = dirEx.Message });
                                }
                            }
                            result.Add((new FileEntry { Name = rf.Name, FullPath = rf.FullPath, Size = rf.Size, Tag = "dir" }, dest));
                        }
                    }
                    catch (Exception ex) { SetStatus(L.Format("st.errorListing", item.Name, L[ex.Message])); }
                }
            }
        }
        return result;
    }

    private async Task<HashSet<int>?> CheckOverwriteAsync(
        List<(FileEntry entry, string destPath)> files,
        bool isUpload,
        bool forceRemoteProbe = false,
        CancellationToken ct = default,
        ConfirmDialog.OverwriteAction? forcedAction = null)
    {
        var conflicts = new List<int>();
        LanClient? remoteSnap = null;
        if (isUpload && forceRemoteProbe)
        {
            await _clientLock.WaitAsync(ct);
            try { remoteSnap = _client; }
            finally { _clientLock.Release(); }
        }

        // N7: Pre-construir índices de paths/tamaños remotos ANTES del bucle.
        // _remoteItems.Any(predicate) en el bucle era O(n×m) — con 100 archivos y 100 entradas = 10.000 comparaciones.
        // HashSet<string> + Dictionary<string,long> lo reducen a O(n+m): 1 scan inicial + O(1) por archivo.
        HashSet<string>? remotePathSet = null;
        Dictionary<string, long>? remoteSizeMap = null;
        if (isUpload)
        {
            remotePathSet = new HashSet<string>(
                _remoteItems.Where(r => !r.IsDirectory).Select(r => r.FullPath),
                StringComparer.OrdinalIgnoreCase);
            remoteSizeMap = _remoteItems
                .Where(r => !r.IsDirectory)
                .ToDictionary(r => r.FullPath, r => r.Size, StringComparer.OrdinalIgnoreCase);
        }

        for (int i = 0; i < files.Count; i++)
        {
            var (entry, dest) = files[i];
            // Items dentro de carpetas expandidas: Tag="dir" → no se puede detectar conflicto (#3)
            if (entry.Tag == "dir") continue;

            // N7: O(1) lookup en lugar de O(m) .Any() scan
            var exists = isUpload
                ? remotePathSet?.Contains(dest) == true
                : File.Exists(dest);
            if (!exists && isUpload && forceRemoteProbe && remoteSnap != null)
            {
                try
                {
                    var st = await remoteSnap.GetStatAsync(dest, ct);
                    exists = st is { Exists: true, IsDirectory: false };
                }
                catch (Exception ex)
                {
                    Log.Debug("transfer", "overwrite-remote-probe-failed", new { path = dest, error = ex.Message });
                }
            }
            if (exists) conflicts.Add(i);
        }

        if (conflicts.Count == 0) return [];

        ConfirmDialog.OverwriteAction action;
        if (forcedAction.HasValue)
        {
            action = forcedAction.Value;
        }
        else
        {
            var dlg = new ConfirmDialog(conflicts.Count, files[conflicts[0]].entry.Name);
            await dlg.ShowDialog(this); // await correcto: espera cierre del dialogo (#1)
            action = await dlg.GetResultAsync();
        }

        if (action == ConfirmDialog.OverwriteAction.Rename)
        {
            // Renombrar destino con sufijo (2), (3)... para archivos en conflicto
            foreach (var i in conflicts)
            {
                var (entry, dest) = files[i];
                var renamed = MakeUniqueDest(dest);
                files[i] = (entry, renamed);
            }
            return [];
        }

        if (action == ConfirmDialog.OverwriteAction.SkipSameSize)
        {
            // Omitir solo archivos que ya existen en el destino con el mismo tamaño
            var toSkip = new System.Collections.Generic.HashSet<int>();
            foreach (var i in conflicts)
            {
                var (entry, dest) = files[i];
                long destSize = -1;
                if (isUpload)
                {
                    // N7: O(1) lookup en lugar de O(m) FirstOrDefault scan
                    if (remoteSizeMap?.TryGetValue(dest, out destSize) == true) { /* destSize ya asignado */ }
                }
                else
                {
                    if (File.Exists(dest))
                    {
                        try { destSize = new System.IO.FileInfo(dest).Length; }
                        catch (Exception ex)
                        {
                            Log.Debug("transfer", "overwrite-local-size-read-failed", new { path = dest, error = ex.Message });
                        }
                    }
                }

                if (destSize != -1 && entry.Size == destSize)
                {
                    toSkip.Add(i);
                }
            }
            return toSkip;
        }

        return action switch
        {
            ConfirmDialog.OverwriteAction.OverwriteAll => [],
            ConfirmDialog.OverwriteAction.SkipAll => [.. conflicts],
            _ => null
        };
    }

    private static string MakeUniqueDest(string dest) => PathSafety.MakeUnique(dest);

}
