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
            var done = Math.Min(totalBytes, _completedBytes + _activeBytes.Values.Sum());
            return new TransferProgressSnapshot(done, totalBytes);
        }
    }

    private async Task TransferAsync(List<FileEntry> items, bool isUpload)
    {
        ref int isFlag = ref (isUpload ? ref _isUploading : ref _isDownloading);
        if (Interlocked.CompareExchange(ref isFlag, 1, 0) == 1) return;

        CancellationTokenSource cts;
        if (isUpload) { _uploadCts?.Dispose(); _uploadCts = new CancellationTokenSource(); cts = _uploadCts; }
        else { _downloadCts?.Dispose(); _downloadCts = new CancellationTokenSource(); cts = _downloadCts; }
        var ct = cts.Token;

        // Capturar IP/puerto en hilo UI antes de entrar en async
        var remoteIp = this.FindControl<TextBox>("txtRemoteIp")!.Text?.Trim() ?? "";
        var portStr = this.FindControl<TextBox>("txtRemotePort")!.Text?.Trim() ?? "8742";
        int.TryParse(portStr, out var remotePort);

        var __pwTitle = isUpload ? L["prog.title.send"] : L["prog.title.receive"];
        _progressWin = new ProgressWindow(__pwTitle, cts);
        _progressWin.Show(this);
        string __finalMsg = "";
        bool __finalErr = false;
        SetTransferButtonsEnabled(false, cancelEnabled: true);
        var __sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var fileList = await ExpandItemsAsync(items, isUpload, ct);
            if (fileList == null || ct.IsCancellationRequested) return;

            // Feature 3: guardar cola persistente al iniciar
            SaveQueue(fileList, isUpload, remoteIp, remotePort);

            var totalBytes = fileList.Sum(x => x.entry.Size);
            var arrow = isUpload ? ">>" : "<<";
            SetStatus(L.Format("st.filesTotal", $"{arrow} {fileList.Count}", fileList.Count > 1 ? L["word.files"] : L["word.file"], FileEntry.FormatSize(totalBytes)));

            var skipSet = await CheckOverwriteAsync(fileList, isUpload);
            if (skipSet == null) return;

            int ok = 0, skip = 0;
            var doneSync = new { Bytes = 0L };
            long totalTransfer = fileList
                .Where((_, i) => !skipSet.Contains(i))
                .Sum(x => x.entry.Size);
            var aggregate = new TransferProgressAggregate(totalTransfer);
            BeginTransferUi(receiving: !isUpload, totalTransfer);

            var failedFiles = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
            var lockDoneBytes = new object();
            long doneBytes = 0; // actualizada dentro de lockDoneBytes

            // -- Pase 1: transferencias paralelas (máx 4 simultáneas) --------
            var transferTasks = new List<Task>();
            for (int fi = 0; fi < fileList.Count; fi++)
            {
                if (ct.IsCancellationRequested) break;
                if (skipSet.Contains(fi)) { skip++; continue; }

                var (entry, destPath) = fileList[fi];
                var taskFi = fi; // capture para closure

                var task = ParallelTransferFileAsync(
                    entry, destPath, isUpload, taskFi, fileList.Count,
                    totalTransfer, arrow, failedFiles, lockDoneBytes, aggregate, remoteIp, remotePort, ct);
                transferTasks.Add(task);
            }

            // Esperar a que todas las transferencias terminen (max 4 simultáneas)
            if (transferTasks.Count > 0)
                await Task.WhenAll(transferTasks);

            // Contar OK y actualizar doneBytes final
            lock (lockDoneBytes)
                doneBytes = totalTransfer - failedFiles.Sum(x => (long)x.entry.Size);
            ok = fileList.Count - skip - failedFiles.Count;

            // -- Pase 2: reintentar archivos fallidos -------------------------
            if (failedFiles.Count > 0 && !ct.IsCancellationRequested)
            {
                SetStatus(L.Format("st.retrying", failedFiles.Count));
                try { await Task.Delay(2000, ct); } catch { goto cleanup; }

                bool reconnected2 = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (!reconnected2) goto cleanup;

                var retryBag = new System.Collections.Concurrent.ConcurrentBag<(FileEntry entry, string destPath)>();
                var retryTasks = new List<Task>();
                int ri = 0;
                foreach (var (entry, destPath) in failedFiles)
                {
                    if (ct.IsCancellationRequested) break;

                    var taskRi = ri++;
                    var task = ParallelTransferFileAsync(
                        entry, destPath, isUpload, taskRi, failedFiles.Count,
                        totalTransfer, $"↺{arrow}", retryBag, lockDoneBytes, aggregate, remoteIp, remotePort, ct);
                    retryTasks.Add(task);
                }

                if (retryTasks.Count > 0)
                    await Task.WhenAll(retryTasks);

                // Re-contar qué pasó
                ok += failedFiles.Count - retryBag.Count;
                failedFiles.Clear();
                foreach (var f in retryBag) failedFiles.Add(f);

                if (failedFiles.Count > 0)
                {
                    var names = string.Join(", ", failedFiles.Select(x => x.entry.Name).Take(3));
                    var extra = failedFiles.Count > 3 ? $" +{failedFiles.Count - 3}" : "";
                    AddHistory(L.Format("hist.incomplete", failedFiles.Count, $"{names}{extra}"), "#FF6B6B");
                }
                else
                {
                    AddHistory(L.Format("hist.retryOk", failedFiles.Count), "#FFD700");
                }

                lock (lockDoneBytes)
                    doneBytes = totalTransfer - failedFiles.Sum(x => (long)x.entry.Size);
            }

        cleanup:
            var msg = skip > 0
                ? L.Format("msg.copiedSkipped", ok, skip, fileList.Count)
                : L.Format("msg.copiedFiles", ok, fileList.Count, FileEntry.FormatSize(doneBytes));
            SetStatus(msg);
            __finalMsg = msg;
            __finalErr = (ok < fileList.Count - skip) || skip > 0;
            if (__finalErr) SetStatusAlert(msg); // requiere lectura: parpadeo llamativo
            var finalSnapshot = aggregate.Snapshot();
            if (!ct.IsCancellationRequested && !__finalErr && ok > 0)
                CompleteTransferUi(receiving: !isUpload, finalSnapshot.DoneBytes, finalSnapshot.TotalBytes, __sw.Elapsed);
            else if (!ct.IsCancellationRequested)
                SuspendTransferUi();
            else
                ResetTransferUi();
            if (ok > 0) {
                AddHistory(L.Format("hist.transferred", arrow, ok, FileEntry.FormatSize(doneBytes)), "#28A745",
                    isUpload ? "send" : "receive", remoteIp, doneBytes, true);
                AuditService.Record(remoteIp, isUpload ? "send" : "receive",
                    ok == 1 && fileList.Count == 1 ? fileList[0].entry.Name : $"[{ok} files]",
                    doneBytes, true, __sw.ElapsedMilliseconds);
            }

            if (isUpload) await RefreshRemoteAsync();
            else await RefreshLocalAsync();
        }
        finally
        {
            // Resetear pausa por si cancelaron mientras pausado
            if (Interlocked.Exchange(ref _isPaused, 0) == 1)
                try { _pauseSemaphore.Release(); } catch { }
            if (_progressWin != null)
            {
                _progressWin.Finish(string.IsNullOrEmpty(__finalMsg) ? L["prog.cancelled"] : __finalMsg, __finalErr);
                _progressWin = null;
            }
            SetTransferButtonsEnabled(true, cancelEnabled: false);
            ref int flagEnd = ref (isUpload ? ref _isUploading : ref _isDownloading);
            Interlocked.Exchange(ref flagEnd, 0);
            ClearQueue(); // Feature 3: transferencia completada (o cancelada)
        }
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
        string remoteIp, int remotePort, CancellationToken ct)
    {
        var prog = new Progress<(long done, long total)>(v =>
        {
            var snapshot = aggregate.Update(progressKey, v.done);
            ReportTransferUi(receiving: !isUpload, snapshot.DoneBytes, totalTransfer > 0 ? totalTransfer : v.total);
        });

        var throttled = new ThrottledProgress<(long, long)>(prog, intervalMs: 200);

        // Feature 2: upload usa _client/_clientLock, download usa _clientDown/_clientLockDown
        var clientLock = isUpload ? _clientLock : _clientLockDown;

        LanClient? snap = null;
        try
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

            if (isUpload)
                await snap.UploadAsync(
                    entry.FullPath,
                    destPath,
                    throttled,
                    ct,
                    (accepted, total) =>
                    {
                        var fromText = FileEntry.FormatSize(accepted);
                        var totalText = FileEntry.FormatSize(total);
                        SetStatus($"Reanudando desde {fromText} / {totalText} ({entry.Name})");
                    });
            else
                await snap.DownloadAsync(entry.FullPath, destPath, throttled, ct);

            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LanCopy] {entry.Name}: {ex.Message}");
            // Invalidar cliente para forzar reconexión. Usa None (no el ct que puede estar
            // cancelado) y solo invalida si sigue siendo el mismo cliente que fallo, para no
            // tirar uno recien reconectado por otra tarea paralela (evita fallos en cascada).
            await _clientLock.WaitAsync(CancellationToken.None);
            try { if (isUpload && _client == snap) { _client?.Dispose(); _client = null; } }
            finally { _clientLock.Release(); }
            await _clientLockDown.WaitAsync(CancellationToken.None);
            try { if (!isUpload && _clientDown == snap) { _clientDown?.Dispose(); _clientDown = null; } }
            finally { _clientLockDown.Release(); }
            return false;
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
            // Feature 1: punto de pausa
            await _pauseSemaphore.WaitAsync(ct);
            if (!ct.IsCancellationRequested) _pauseSemaphore.Release();
            if (ct.IsCancellationRequested) return;

            // Limitar a máx 4 simultáneas
            await _transferSemaphore.WaitAsync(ct);
            semaphoreHeld = true;

            var progressKey = fi;

            bool fileOk = await DoTransferFileAsync(
                entry, destPath, isUpload, fi, total, totalTransfer, aggregate, progressKey, remoteIp, remotePort, ct);

            if (!fileOk && !ct.IsCancellationRequested)
            {
                aggregate.Fail(progressKey);
                SetStatus(L.Format("st.errorReconnecting", entry.Name));
                bool reconnected = await TryReconnectAsync(remoteIp, remotePort, ct);
                if (reconnected)
                    fileOk = await DoTransferFileAsync(
                        entry, destPath, isUpload, fi, total, totalTransfer, aggregate, progressKey, remoteIp, remotePort, ct);
            }

            if (fileOk)
            {
                var snapshot = aggregate.Complete(progressKey, entry.Size);
                ReportTransferUi(receiving: !isUpload, snapshot.DoneBytes, snapshot.TotalBytes);
                lock (lockDoneBytes)
                {
                    var failed = failedBag.Sum(x => (long)x.Item1.Size);
                    var doneNow = totalTransfer - failed;
                }
            }
            else if (!ct.IsCancellationRequested)
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
                    catch { }
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
                            if (!TrySafeCombineUnder(_localPath, out var dest, item.Name, rf.Name)) continue;
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
        List<(FileEntry entry, string destPath)> files, bool isUpload)
    {
        var conflicts = new List<int>();
        for (int i = 0; i < files.Count; i++)
        {
            var (entry, dest) = files[i];
            // Items dentro de carpetas expandidas: Tag="dir" → no se puede detectar conflicto (#3)
            if (entry.Tag == "dir") continue;

            bool exists = isUpload
                ? _remoteItems.Any(r => !r.IsDirectory && string.Equals(r.FullPath, dest, StringComparison.OrdinalIgnoreCase))
                : File.Exists(dest);
            if (exists) conflicts.Add(i);
        }

        if (conflicts.Count == 0) return [];

        var dlg = new ConfirmDialog(conflicts.Count, files[conflicts[0]].entry.Name);
        await dlg.ShowDialog(this); // await correcto: espera cierre del dialogo (#1)
        var action = await dlg.GetResultAsync();

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

        return action switch
        {
            ConfirmDialog.OverwriteAction.OverwriteAll => [],
            ConfirmDialog.OverwriteAction.SkipAll => [.. conflicts],
            _ => null
        };
    }

    private static string MakeUniqueDest(string dest) => PathSafety.MakeUnique(dest);

}
