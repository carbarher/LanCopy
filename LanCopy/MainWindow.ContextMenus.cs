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
        await TransferAsync(items, isUpload: true);
    }

    private async void LocalCtx_Rename(object? sender, RoutedEventArgs e)
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

    private async void LocalCtx_Delete(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList");
        if (items.Count == 0) return;

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

    private async void LocalCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("localList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var ct = CancellationToken.None;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // Paralelo: max 4 simultáneos (evita saturar cliente)
        var tasks = items.Select(item => VerifyLocalItemAsync(item, toleranceTicks, results, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog(L["verify.titleLR"], text.TrimEnd());
    }

    private async Task VerifyLocalItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results, CancellationToken ct)
    {
        try
        {
            var remotePath = string.IsNullOrEmpty(_remotePath)
                ? item.Name
                : Path.Combine(_remotePath, item.Name).Replace('\\', '/');

            var fi = new FileInfo(item.FullPath);

            await _clientLock.WaitAsync(ct);
            LanClient.RemoteStat? stat;
            try { stat = await _client!.GetStatAsync(remotePath, ct); }
            finally { _clientLock.Release(); }

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

            await _clientLock.WaitAsync(ct);
            string? remoteHash;
            bool usingSha256;
            try
            {
                remoteHash = await _client!.GetSha256Async(remotePath, ct);
                usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
                if (!usingSha256) remoteHash = await _client.GetSha1Async(remotePath, ct);
            }
            finally { _clientLock.Release(); }

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
        var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
        SetStatus(L["st.pathsCopied"]);
    }

    // ══ Context menus — Remote ════════════════════════════════════════════════════

    private void RemoteCtx_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var items = GetSelectedItems("remoteList");
        bool any = items.Count > 0;
        bool single = items.Count == 1;
        bool connected = _client != null;

        this.FindControl<MenuItem>("ctxRemoteReceive")!.IsEnabled = any && connected;
        this.FindControl<MenuItem>("ctxRemoteRename")!.IsEnabled = single && connected;
        this.FindControl<MenuItem>("ctxRemoteDelete")!.IsEnabled = any && connected;
        this.FindControl<MenuItem>("ctxRemoteVerify")!.IsEnabled = any && !items.Any(x => x.IsDirectory) && connected;
    }

    private async void RemoteCtx_Receive(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;
        await TransferAsync(items, isUpload: false);
    }

    private async void RemoteCtx_Rename(object? sender, RoutedEventArgs e)
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
            try { await _client!.RenameAsync(item.FullPath, newName); }
            finally { _clientLock.Release(); }

            SafeFileOps.Audit("remote-rename", item.FullPath, "ok", $"to:{newName}");
            await RefreshRemoteAsync();
        }
        catch (Exception ex)
        {
            SafeFileOps.Audit("remote-rename", item.FullPath, "error", ex.Message);
            SetStatus(L.Format("st.renameError", L[ex.Message]));
        }
    }

    private async void RemoteCtx_Delete(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList");
        if (items.Count == 0) return;

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

        foreach (var item in items)
        {
            __delIdx++;
            __delWin.SetLine(item.Name);
            __delWin.SetProgress(__delTotal > 0 ? __delIdx * 100.0 / __delTotal : 100, $"{__delIdx}/{__delTotal}");
            try
            {
                await _clientLock.WaitAsync();
                try { await _client!.DeleteAsync(item.FullPath); }
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

    private async void RemoteCtx_Verify(object? sender, RoutedEventArgs e)
    {
        if (_client == null) { SetStatus(L["st.connectFirst"]); return; }
        var items = GetSelectedItems("remoteList").Where(x => !x.IsDirectory).ToList();
        if (items.Count == 0) return;

        SetStatus(L["st.verifying"]);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var ct = CancellationToken.None;
        var toleranceTicks = TimeSpan.FromSeconds(2).Ticks;

        // Paralelo: max 4 simultáneos
        var tasks = items.Select(item => VerifyRemoteItemAsync(item, toleranceTicks, results, ct)).ToList();
        await Task.WhenAll(tasks);

        var sortedResults = results.OrderBy(r => r).ToList();
        var text = string.Join(Environment.NewLine, sortedResults);
        await ShowInfoDialog(L["verify.titleRL"], text.TrimEnd());
    }

    private async Task VerifyRemoteItemAsync(FileEntry item, long toleranceTicks,
        System.Collections.Concurrent.ConcurrentBag<string> results, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_localPath, item.Name);

            await _clientLock.WaitAsync(ct);
            LanClient.RemoteStat? stat;
            try { stat = await _client!.GetStatAsync(item.FullPath, ct); }
            finally { _clientLock.Release(); }

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

            await _clientLock.WaitAsync(ct);
            string? remoteHash;
            bool usingSha256;
            try
            {
                remoteHash = await _client!.GetSha256Async(item.FullPath, ct);
                usingSha256 = !string.IsNullOrWhiteSpace(remoteHash);
                if (!usingSha256) remoteHash = await _client.GetSha1Async(item.FullPath, ct);
            }
            finally { _clientLock.Release(); }

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
        var text = string.Join(Environment.NewLine, items.Select(x => x.FullPath));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
        SetStatus(L["st.pathsCopied"]);
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
