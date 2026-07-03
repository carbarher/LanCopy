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
    private MenuItem? ResolveMenuItem(ContextMenu? menu, string name) =>
        menu?.Items?.OfType<MenuItem>().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal))
        ?? this.FindControl<MenuItem>(name);

    // ══ Context menus — Local ═════════════════════════════════════════════════════

    private void LocalCtx_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var items = GetSelectedItems("localList");
        bool any = items.Count > 0;
        bool single = items.Count == 1;
        bool anyInvalid = items.Any(x => !SafeFileOps.TryValidateMutationPath(x.FullPath, out _, out _));

        this.FindControl<MenuItem>("ctxLocalSend")!.IsEnabled = any && _client != null;
        this.FindControl<MenuItem>("ctxLocalRename")!.IsEnabled = single && !anyInvalid;
        this.FindControl<MenuItem>("ctxLocalDelete")!.IsEnabled = any;
        this.FindControl<MenuItem>("ctxLocalVerify")!.IsEnabled = any && !items.Any(x => x.IsDirectory) && _client != null;
    }

    private async void LocalCtx_Send(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;
        try { await TransferAsync(items, isUpload: true); }
        catch (Exception ex) { Log.Warn("ctx-menu", "local-send-unexpected", new { error = ex.Message }); }
    }

    private async void LocalCtx_Rename(object? sender, RoutedEventArgs e)
    {
        try
        {
        var items = GetSelectedItems("localList");
        if (items.Count != 1) return;
        var item = items[0];

        if (!SafeFileOps.TryValidateMutationPath(item.FullPath, out var sourcePath, out var reason))
        {
            SetStatus(L.Format("st.blocked", L[reason]));
            SafeFileOps.Audit("rename", item.FullPath, "blocked", reason);
            return;
        }

        var dlg = new InputDialog(L["dlg.rename.title"], L["dlg.rename.prompt"], item.Name);
        _ = dlg.ShowDialog(this);
        var newName = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
        if (newName is "." or ".." || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || newName.StartsWith(" ") || newName.EndsWith(" ") || newName.EndsWith("."))
            { SetStatus(L["st.invalidName"]); return; }

        if (SafeFileOps.IsOnCooldown($"local-rename:{sourcePath}", 2))
        {
            SetStatus(L["st.cooldown"]);
            SafeFileOps.Audit("rename", sourcePath, "blocked", "cooldown");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(sourcePath)!;
            var destPath = Path.Combine(dir, newName);
            if (!SafeFileOps.TryValidateMutationPath(destPath, out _, out var destReason, requireExists: false))
            {
                SetStatus(L.Format("st.destBlocked", destReason));
                SafeFileOps.Audit("rename", sourcePath, "blocked", $"dest:{destReason}");
                return;
            }

            if (item.IsDirectory) Directory.Move(sourcePath, destPath);
            else File.Move(sourcePath, destPath);

            SafeFileOps.Audit("rename", sourcePath, "ok", $"to:{destPath}");
            await RefreshLocalAsync();
        }
        catch (Exception ex)
        {
            SafeFileOps.Audit("rename", sourcePath, "error", ex.Message);
            SetStatus(L.Format("st.renameError", L[ex.Message]));
        }
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "local-rename-unexpected", new { error = ex.Message });
        }
    }

    private async void LocalCtx_Delete(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;

        try
        {
        var allowed = new List<(FileEntry item, string path)>();
        var blocked = new List<string>();

        foreach (var item in items)
        {
            if (SafeFileOps.TryValidateMutationPath(item.FullPath, out var normalized, out var reason))
                allowed.Add((item, normalized));
            else
            {
                blocked.Add($"{item.Name}: {L[reason]}");
                SafeFileOps.Audit("delete", item.FullPath, "blocked", reason);
            }
        }

        if (allowed.Count == 0)
        {
            await ShowInfoDialog(L["del.blockedTitle"], string.Join(Environment.NewLine, blocked));
            return;
        }

        var msg = L.Format("del.confirmLocal", allowed.Count);
        if (!await MessageBox(msg, L["del.confirmTitle"])) return;

        if (SafeFileOps.IsHighRiskDelete(allowed.Select(x => x.path).ToList()))
        {
            var confirmDlg = new InputDialog(L["dlg.hard.title"], L.Format("dlg.hard.prompt", SafeFileOps.HardConfirmToken), "");
            _ = confirmDlg.ShowDialog(this);
            var token = await confirmDlg.GetResultAsync();
            if (!string.Equals(token, SafeFileOps.HardConfirmToken, StringComparison.Ordinal))
            {
                SetStatus(L["st.deleteCancelled"]);
                return;
            }
        }

        var __delWin = new ProgressWindow(L["prog.title.delete"]);
        _progressWin = __delWin;
        __delWin.Show(this);
        int __delTotal = allowed.Count, __delIdx = 0;
        int ok = 0, cooldown = 0, err = 0;
        var lines = new List<string>();
        try
        {
        foreach (var (item, path) in allowed)
        {
            __delIdx++;
            __delWin.SetLine(item.Name);
            __delWin.SetProgress(__delTotal > 0 ? __delIdx * 100.0 / __delTotal : 100, $"{__delIdx}/{__delTotal}");
            var key = $"local-delete:{path}";
            if (SafeFileOps.IsOnCooldown(key, 2))
            {
                cooldown++;
                lines.Add($"⏳ {item.Name} — {L["word.cooldown"]}");
                SafeFileOps.Audit("delete", path, "blocked", "cooldown");
                continue;
            }

            if (SafeFileOps.TryMoveToTrash(path, out var moved, out var moveErr))
            {
                ok++;
                lines.Add($"🗑️ {item.Name} -> {moved}");
                SafeFileOps.Audit("delete", path, "ok", $"trash:{moved}");
                AuditService.Record("127.0.0.1", "delete", path, 0, true, 0, $"trash:{moved}");
            }
            else
            {
                err++;
                lines.Add($"⚠️ {item.Name} — {moveErr}");
                SafeFileOps.Audit("delete", path, "error", moveErr);
            }
        }
        __delWin.Finish(L.Format("st.deleteLocalResult", ok, blocked.Count + cooldown, err), err > 0);
        _progressWin = null;

        if (blocked.Count > 0) lines.InsertRange(0, blocked.Select(x => $"🔒 {x}"));

        await RefreshLocalAsync();
        await ShowInfoDialog(L["del.summaryLocalTitle"], L.Format("del.summaryCounts", ok, blocked.Count + cooldown, err) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines));
        var __delResult = L.Format("st.deleteLocalResult", ok, blocked.Count + cooldown, err);
        if (err > 0 || blocked.Count + cooldown > 0) SetStatusAlert(__delResult); else SetStatus(__delResult);
        }
        finally
        {
            if (_progressWin != null)
            {
                _progressWin.Finish(L["prog.cancelled"], isError: true);
                _progressWin = null;
            }
        }
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "local-delete-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private async void LocalCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        try
        {
        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        // C12-FIX: timeout de 120s para todas las tareas — CancellationToken.None causaba
        // Task.WhenAll bloqueado indefinidamente si el server estaba lento o la conexión caía.
        using var verifyCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = verifyCts.Token;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // BUG-FIX: capturar IP/puerto en el UI thread antes de lanzar tasks
        // para poder crear clientes temporales independientes por task (verdadero paralelismo).
        // Antes se usaba _clientLock (SemaphoreSlim(1,1)) dentro de cada task, convirtiendo
        // la verificación paralela en completamente secuencial (N archivos = N × latencia).
        // BUG-FIX-2: leer Host/Port bajo _clientLock — _client puede nullarse entre el guard L227
        // y el acceso aquí (async void, sin lock previo). Snapshot atómico evita NullReferenceException.
        string verifyIp; int verifyPort;
        await _clientLock.WaitAsync();
        try
        {
            if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
            verifyIp = _client.Host; verifyPort = _client.Port;
        }
        finally { _clientLock.Release(); }

        // Paralelo real: cada task crea su propio cliente temporal (LanClient es stateless)
        var tasks = items.Select(item => VerifyLocalItemAsync(item, toleranceTicks, results, verifyIp, verifyPort, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog(L["verify.titleLR"], text.TrimEnd());
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "local-verify-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private async Task VerifyLocalItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results,
        string remoteIp, int remotePort, CancellationToken ct)
    {
        try
        {
            var remotePath = string.IsNullOrEmpty(_remotePath)
                ? item.Name
                : Path.Combine(_remotePath, item.Name).Replace('\\', '/');

            var fi = new FileInfo(item.FullPath);

            // BUG-FIX: cliente temporal por task — verdadero paralelismo sin lock
            using var cli = MakeClient(remoteIp, remotePort);
            var stat = await cli.GetStatAsync(remotePath, ct);

            if (stat is null || !stat.Exists)
            {
                results.Add($"❓ {item.Name} — {L["verify.notInRemote"]}");
                return;
            }
            if (stat.IsDirectory)
            {
                results.Add($"⚠️ {item.Name} — {L["verify.remoteIsDir"]}");
                return;
            }

            var sizeMatch = fi.Length == stat.Size;
            var timeMatch = Math.Abs(fi.LastWriteTimeUtc.Ticks - stat.LastWriteUtcTicks) <= toleranceTicks;
            if (!sizeMatch || !timeMatch)
            {
                results.Add($"❌ {item.Name} — {L["verify.quickDiff"]}");
                return;
            }

            await using var fs = File.OpenRead(item.FullPath);
            var localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();

            string? remoteHash;
            bool usingSha256;
            remoteHash = await cli.GetSha256Async(remotePath, ct);
            usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
            if (!usingSha256) remoteHash = await cli.GetSha1Async(remotePath, ct);

            if (remoteHash == null) results.Add($"❓ {item.Name} — {L["verify.noRemoteHash"]}");
            else if (usingSha256 && string.Equals(localSha256, remoteHash, StringComparison.OrdinalIgnoreCase))
                results.Add($"✅ {item.Name} — {L["verify.identicalSha256"]}");
            else if (!usingSha256)
            {
                fs.Seek(0, SeekOrigin.Begin);
                var localSha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                if (string.Equals(localSha1, remoteHash, StringComparison.OrdinalIgnoreCase))
                    results.Add($"✅ {item.Name} — {L["verify.identicalSha1"]}");
                else results.Add($"❌ {item.Name} — {L["verify.hashDiff"]}");
            }
            else results.Add($"❌ {item.Name} — {L["verify.hashDiff"]}");
        }
        catch (Exception ex)
        {
            results.Add($"⚠️ {item.Name} — {L.Format("verify.error", L[ex.Message])}");
        }
    }

    private async void LocalCtx_CopyPath(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;
        try
        {
            var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
            SetStatus(L["st.pathsCopied"]);
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "copy-path-failed", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    // ══ Context menus — Remote ════════════════════════════════════════════════════

    private void RemoteCtx_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var items = GetSelectedItems("remoteList");
        bool any = items.Count > 0;
        bool single = items.Count == 1;
        bool connected = _client != null;
        var menu = sender as ContextMenu;

        var receive = ResolveMenuItem(menu, "ctxRemoteReceive");
        if (receive != null) receive.IsEnabled = any && connected;

        var createFolder = ResolveMenuItem(menu, "ctxRemoteCreateFolder");
        if (createFolder != null)
        {
            createFolder.IsVisible = true;
            createFolder.Header = string.IsNullOrWhiteSpace(L["ctx.newfolder"]) ? "Create folder" : L["ctx.newfolder"];
            createFolder.IsEnabled = connected;
        }

        var rename = ResolveMenuItem(menu, "ctxRemoteRename");
        if (rename != null) rename.IsEnabled = single && connected;

        var delete = ResolveMenuItem(menu, "ctxRemoteDelete");
        if (delete != null) delete.IsEnabled = any && connected;

        var verify = ResolveMenuItem(menu, "ctxRemoteVerify");
        if (verify != null) verify.IsEnabled = any && !items.Any(x => x.IsDirectory) && connected;
    }

    private async void RemoteCtx_Receive(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;
        try { await TransferAsync(items, isUpload: false); }
        catch (Exception ex) { Log.Warn("ctx-menu", "remote-receive-unexpected", new { error = ex.Message }); }
    }

    private async void RemoteCtx_Rename(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count != 1) return;
        var item = items[0];

        var dlg = new InputDialog(L["dlg.rename.titleRemote"], L["dlg.rename.prompt"], item.Name);
        _ = dlg.ShowDialog(this);
        var newName = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
        if (newName is "." or ".." || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || newName.StartsWith(" ") || newName.EndsWith(" ") || newName.EndsWith("."))
            { SetStatus(L["st.invalidName"]); return; }

        try
        {
            await _clientLock.WaitAsync();
            // C11-FIX: re-verificar _client bajo lock — el await del diálogo (L400) deja
            // una ventana de tiempo en la que el usuario puede desconectarse y poner _client = null.
            // Sin este check, _client! lanzaba NullReferenceException con mensaje genérico.
            if (_client == null)
            {
                _clientLock.Release();
                SetStatus(L["st.connectFirst"]);
                return;
            }
            using var renameCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await _client.RenameAsync(item.FullPath, newName, renameCts.Token); }
            finally { _clientLock.Release(); }

            SafeFileOps.Audit("remote-rename", item.FullPath, "ok", $"to:{newName}");
            await RefreshRemoteDebounced(); // M3: debounce 150ms — post-rename no requiere feedback inmediato
        }
        catch (Exception ex)
        {
            SafeFileOps.Audit("remote-rename", item.FullPath, "error", ex.Message);
            SetStatus(L.Format("st.renameError", L[ex.Message]));
        }
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "remote-rename-unexpected", new { error = ex.Message });
        }
    }

    private async void RemoteCtx_CreateFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }

        var dlg = new InputDialog(L["dlg.newfolder.titleRemote"], L["dlg.newfolder.prompt"], "");
        _ = dlg.ShowDialog(this);
        var folderName = await dlg.GetResultAsync();
        if (string.IsNullOrWhiteSpace(folderName)) return;

        if (folderName is "." or ".." || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || folderName.StartsWith(" ") || folderName.EndsWith(" ") || folderName.EndsWith("."))
            { SetStatus(L["st.invalidName"]); return; }

        var remotePath = string.IsNullOrEmpty(_remotePath)
            ? folderName
            : Path.Combine(_remotePath, folderName).Replace('\\', '/');

        try
        {
            await _clientLock.WaitAsync();
            // C11-FIX: re-verificar _client bajo lock — el await del diálogo (L445) deja
            // una ventana de tiempo en la que el usuario puede desconectarse y poner _client = null.
            if (_client == null)
            {
                _clientLock.Release();
                SetStatus(L["st.connectFirst"]);
                return;
            }
            using var mkdirCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await _client.CreateDirectoryAsync(remotePath, mkdirCts.Token); }
            finally { _clientLock.Release(); }

            SetStatus(L.Format("st.folderCreatedRemote", folderName));
            await RefreshRemoteDebounced(); // M3: debounce 150ms — post-mkdir no requiere feedback inmediato
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("st.createFolderError", L[ex.Message]));
        }
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "remote-create-folder-unexpected", new { error = ex.Message });
        }
    }

    private async void RemoteCtx_Delete(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;

        try
        {
        if (!await MessageBox(L.Format("del.confirmRemote", items.Count), L["del.confirmRemoteTitle"])) return;

        if (items.Count >= 20 || items.Any(x => x.IsDirectory))
        {
            var confirmDlg = new InputDialog(L["dlg.hard.title"], L.Format("dlg.hard.prompt", SafeFileOps.HardConfirmToken), "");
            _ = confirmDlg.ShowDialog(this);
            var token = await confirmDlg.GetResultAsync();
            if (!string.Equals(token, SafeFileOps.HardConfirmToken, StringComparison.Ordinal))
            {
                SetStatus(L["st.deleteCancelledRemote"]);
                return;
            }
        }

        var __delWin = new ProgressWindow(L["prog.title.deleteRemote"]);
        _progressWin = __delWin;
        __delWin.Show(this);
        int __delTotal = items.Count, __delIdx = 0;
        int ok = 0, err = 0;
        var lines = new List<string>();
        try
        {
        foreach (var item in items)
        {
            __delIdx++;
            __delWin.SetLine(item.Name);
            __delWin.SetProgress(__delTotal > 0 ? __delIdx * 100.0 / __delTotal : 100, $"{__delIdx}/{__delTotal}");
            try
            {
                await _clientLock.WaitAsync();
                // C11-FIX: re-verificar _client bajo lock — los await de MessageBox (L493) y
                // confirmDlg (L499) dejan ventana de tiempo para que el usuario se desconecte.
                if (_client == null)
                {
                    _clientLock.Release();
                    err++;
                    lines.Add($"❌ {item.Name} — {L["st.connectFirst"]}");
                    SafeFileOps.Audit("remote-delete", item.FullPath, "blocked", "disconnected-mid-delete");
                    continue;
                }
                using var deleteCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await _client.DeleteAsync(item.FullPath, deleteCts.Token); }
                finally { _clientLock.Release(); }

                ok++;
                lines.Add($"🗑️ {item.Name} — {L["word.sentRemoteTrash"]}");
                SafeFileOps.Audit("remote-delete", item.FullPath, "ok", "trash-remote");
            }
            catch (Exception ex)
            {
                err++;
                lines.Add($"⚠️ {item.Name} — {L[ex.Message]}");
                SafeFileOps.Audit("remote-delete", item.FullPath, "error", ex.Message);
            }
        }
        __delWin.Finish(L.Format("st.deleteRemoteResult", ok, err), err > 0);
        _progressWin = null;

        await RefreshRemoteAsync();
        await ShowInfoDialog(L["del.summaryRemoteTitle"], L.Format("del.summaryCountsRemote", ok, err) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines));
        var __delRResult = L.Format("st.deleteRemoteResult", ok, err);
        if (err > 0) SetStatusAlert(__delRResult); else SetStatus(__delRResult);
        }
        finally
        {
            // BUG-FIX: garantizar que el ProgressWindow se cierra aunque ocurra una excepción
            // inesperada después de Show(), evitando ventanas zombi que bloquean la UI.
            if (_progressWin != null)
            {
                _progressWin.Finish(L["prog.cancelled"], isError: true);
                _progressWin = null;
            }
        }
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "remote-delete-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private async void RemoteCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        try
        {
        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        // C12-FIX: timeout de 120s — sin CT, verify de archivos grandes podía bloquear indefinidamente.
        using var verifyCts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = verifyCts2.Token;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // BUG-FIX: capturar IP/puerto en el UI thread antes de lanzar tasks paralelas.
        // BUG-FIX-2: leer Host/Port bajo _clientLock — _client puede nullarse entre el guard L538
        // y el acceso aquí (async void, sin lock previo). Snapshot atómico evita NullReferenceException.
        string verifyIp; int verifyPort;
        await _clientLock.WaitAsync();
        try
        {
            if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
            verifyIp = _client.Host; verifyPort = _client.Port;
        }
        finally { _clientLock.Release(); }

        // Paralelo real: cada task crea su propio cliente temporal
        var tasks = items.Select(item => VerifyRemoteItemAsync(item, toleranceTicks, results, verifyIp, verifyPort, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog(L["verify.titleRL"], text.TrimEnd());
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "remote-verify-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    private async Task VerifyRemoteItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results,
        string remoteIp, int remotePort, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_localPath, item.Name);

            // BUG-FIX: cliente temporal por task — verdadero paralelismo sin lock
            using var cli = MakeClient(remoteIp, remotePort);
            var stat = await cli.GetStatAsync(item.FullPath, ct);

            if (stat is null || !stat.Exists) { results.Add($"❓ {item.Name} — {L["verify.notAvailRemote"]}"); return; }
            if (stat.IsDirectory) { results.Add($"⚠️ {item.Name} — {L["verify.remoteIsDir"]}"); return; }
            if (!File.Exists(localPath)) { results.Add($"❓ {item.Name} — {L["verify.notLocal"]}"); return; }

            var fi = new FileInfo(localPath);
            var sizeMatch = fi.Length == stat.Size;
            var timeMatch = Math.Abs(fi.LastWriteTimeUtc.Ticks - stat.LastWriteUtcTicks) <= toleranceTicks;
            if (!sizeMatch || !timeMatch)
            {
                results.Add($"❌ {item.Name} — {L["verify.quickDiff"]}");
                return;
            }

            await using var fs = File.OpenRead(localPath);
            var localSha256 = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();

            string? remoteHash;
            bool usingSha256;
            remoteHash = await cli.GetSha256Async(item.FullPath, ct);
            usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
            if (!usingSha256) remoteHash = await cli.GetSha1Async(item.FullPath, ct);

            if (remoteHash == null) results.Add($"❓ {item.Name} — {L["verify.noRemoteHash"]}");
            else if (usingSha256 && string.Equals(localSha256, remoteHash, StringComparison.OrdinalIgnoreCase))
                results.Add($"✅ {item.Name} — {L["verify.identicalSha256"]}");
            else if (!usingSha256)
            {
                fs.Seek(0, SeekOrigin.Begin);
                var localSha1 = Convert.ToHexString(await SHA1.HashDataAsync(fs, ct)).ToLowerInvariant();
                if (string.Equals(localSha1, remoteHash, StringComparison.OrdinalIgnoreCase))
                    results.Add($"✅ {item.Name} — {L["verify.identicalSha1"]}");
                else results.Add($"❌ {item.Name} — {L["verify.hashDiff"]}");
            }
            else results.Add($"❌ {item.Name} — {L["verify.hashDiff"]}");
        }
        catch (Exception ex)
        {
            results.Add($"⚠️ {item.Name} — {L.Format("verify.error", L[ex.Message])}");
        }
    }

    private async void RemoteCtx_CopyPath(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;
        try
        {
            var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
            SetStatus(L["st.pathsCopied"]);
        }
        catch (Exception ex)
        {
            Log.Warn("ctx-menu", "copy-path-failed", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    // ══ Helpers UI ════════════════════════════════════════════════════════════════

    private async Task<bool> MessageBox(string message, string? title = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window
        {
            Title = title ?? L["dlg.confirm"],
            Width = 420,
            Height = 160,
            CanResize = false,
            Background = SolidColorBrush.Parse("#2D2D30"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var btnNo = new Button { Content = L["dlg.cancel"], Background = SolidColorBrush.Parse("#3E3E42"), Foreground = Brushes.White, Padding = new Thickness(10, 5) };
        var btnYes = new Button { Content = L["dlg.accept"], Background = SolidColorBrush.Parse("#C0392B"), Foreground = Brushes.White, Padding = new Thickness(10, 5) };
        btnNo.Click += (_, _) => { tcs.TrySetResult(false); dlg.Close(); };
        btnYes.Click += (_, _) => { tcs.TrySetResult(true); dlg.Close(); };
        btns.Children.Add(btnNo);
        btns.Children.Add(btnYes);
        panel.Children.Add(btns);
        dlg.Content = panel;
        dlg.Closing += (_, _) => tcs.TrySetResult(false);
        await dlg.ShowDialog(this);
        return await tcs.Task;
    }

    private async Task ShowInfoDialog(string title, string content)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 560,
            Height = 360,
            Background = SolidColorBrush.Parse("#2D2D30"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        var scroll = new ScrollViewer { MaxHeight = 260 };
        scroll.Content = new TextBlock
        {
            Text = content,
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Courier New,monospace")
        };
        panel.Children.Add(scroll);
        var btnClose = new Button
        {
            Content = L["dlg.close"],
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(12, 5)
        };
        btnClose.Click += (_, _) => dlg.Close();
        panel.Children.Add(btnClose);
        dlg.Content = panel;
        await dlg.ShowDialog(this);
    }

}
