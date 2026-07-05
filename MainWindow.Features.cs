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
using LanCopy.Dialogs;
using LanCopy.Services;

namespace LanCopy;

public partial class MainWindow
{
    // UX para usuarios sin conocimientos de red

    // Aplica el modo de interfaz: en modo simple se oculta el panel avanzado
    // (puertos, IP manual, perfiles, checkboxes de seguridad, etc.).
    private void ApplyUiMode()
    {
        var adv = this.FindControl<StackPanel>("advancedPanel");
        if (adv != null) adv.IsVisible = _advancedMode;
        var btn = this.FindControl<Button>("btnAdvancedToggle");
        if (btn != null) btn.Content = _advancedMode ? L["btn.advanced.hide"] : L["btn.advanced.show"];
    }

    private void ToggleAdvanced_Click(object? sender, RoutedEventArgs e)
    {
        _advancedMode = !_advancedMode;
        ApplyUiMode();
        try
        {
            SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742");
        }
        catch (Exception ex)
        {
            Log.Warn("ui", "save-settings-after-advanced-toggle-failed", new { error = ex.Message });
        }
    }

    // Abre el asistente de bienvenida (tambien accesible con el boton de ayuda).
    private async void ShowWelcome_Click(object? sender, RoutedEventArgs e)
    {
        try { await new WelcomeDialog().ShowDialog(this); }
        catch (Exception ex) { Log.Warn("ui", "show-welcome-dialog-failed", new { error = ex.Message }); }
    }

    // Q3: helper extraído; Q1/B7: usar HealthInfo? en vez de dynamic? para type safety
    private async Task<LanCopy.Services.LanClient.RemoteHealth?> TryGetRemoteHealthAsync(string remoteIp, int remotePort)
    {
        try
        {
            using var cli = MakeClient(remoteIp, remotePort);
            // C9-FIX: timeout de 5s — sin CT, GetHealthAsync dependía del KeepAlive TCP (~30s)
            using var healthCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await cli.GetHealthAsync(healthCts.Token);
        }
        catch (Exception ex)
        {
            Log.Debug("diag", "remote-health-probe-failed", new { remoteIp, remotePort, error = ex.Message });
            return null;
        }
    }

        // Diagnostico "no veo el otro PC": comprueba red y equipos detectados y lo
    // explica en lenguaje llano, sin tecnicismos.
    private async void Diagnose_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        var sb = new System.Text.StringBuilder();
        var ip = _server.LocalIp ?? "";
        var peers = _discovery?.GetPeers() ?? [];

        // Estado del servidor de este equipo.
        sb.AppendLine(L.Format("diag.thispc", $"{ip}:{_server.Port}"));
        sb.AppendLine();

        bool noNet = string.IsNullOrEmpty(ip) || ip == "127.0.0.1" || ip == "0.0.0.0";
        bool apipa = ip.StartsWith("169.254.", StringComparison.Ordinal);

        if (noNet)
        {
            sb.AppendLine(L["diag.nonet"]);
        }
        else if (apipa)
        {
            sb.AppendLine(L["diag.apipa"]);
        }
        else
        {
            sb.AppendLine(L.Format("diag.peers", peers.Count));
            if (peers.Count == 0)
                sb.AppendLine(L["diag.nopeers"]);
            else
                foreach (var p in peers) sb.AppendLine($"  - {p.Name}  ({p.Ip}:{p.Port})");
        }

        var remoteIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var remotePortText = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (!string.IsNullOrWhiteSpace(remoteIp) && int.TryParse(remotePortText, out var remotePort))
        {
            // Q6: usar el helper extraído en lugar de código inline duplicado
            var health = await TryGetRemoteHealthAsync(remoteIp, remotePort);
            sb.AppendLine();
            if (health != null)
            {
                sb.AppendLine(L["diag.remoteHealth"]); // U1: era hardcodeado en español
                sb.AppendLine(L.Format("diag.connActive", health.ConnCurrent, health.ConnLimit));
                sb.AppendLine(L.Format("diag.perIpLimit", health.PerIpLimit, health.ActiveIps));
                sb.AppendLine(L.Format("diag.pinFails", health.PinFailsTracked));
                sb.AppendLine(L.Format("diag.hashCache", health.HashCacheEntries));
                sb.AppendLine(L.Format("diag.rateLimit", health.CommandRateLimit, health.CommandRateWindowSeconds, health.CommandRateTracked));
            }
            else
            {
                sb.AppendLine(L.Format("diag.remoteUnreachable", $"{remoteIp}:{remotePort}"));
            }
        }

        try { await new InfoDialog(L["diag.title"], sb.ToString()).ShowDialog(this); }
        catch (Exception ex) { Log.Warn("diag", "show-diagnose-dialog-failed", new { error = ex.Message }); }
        }
        catch (Exception ex) { Log.Warn("diag", "diagnose-unexpected", new { error = ex.Message }); }
    }

    private async void ExportDiagnostics_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var diagDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanCopy", "diag");
            Directory.CreateDirectory(diagDir);
            var zipPath = Path.Combine(diagDir, $"lancopy-diag-{stamp}.zip");
            var tempDir = Path.Combine(Path.GetTempPath(), "LanCopyDiag_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"LanCopy diagnostics generated: {DateTime.Now:O}");
                summary.AppendLine($"Version: {typeof(MainWindow).Assembly.GetName().Version}");
                summary.AppendLine($"Local endpoint: {_server.LocalIp}:{_server.Port}");
                summary.AppendLine($"Connected: {(await IsConnectedAsync() ? "yes" : "no")}");
                summary.AppendLine($"TLS: {_tlsEnabled}; Compress: {_compressEnabled}; RestrictShareRoot: {_restrictShareRoot}");
                summary.AppendLine($"SafeMode: {_safeModeEnabled}; SafeModeNoDelete: {_safeModeNoRemoteDelete}; ReadOnly: {_readOnly}; RequireApproval: {_requireApproval}");
                var peers = _discovery?.GetPeers() ?? [];
                summary.AppendLine($"Discovered peers: {peers.Count}");
                foreach (var p in peers) summary.AppendLine($"  - {p.Name} ({p.Ip}:{p.Port})");

                var remoteIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                var remotePortText = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
                if (!string.IsNullOrWhiteSpace(remoteIp) && int.TryParse(remotePortText, out var remotePort))
                {
                    try
                    {
                        using var cli = MakeClient(remoteIp, remotePort);
                        // C9-FIX: timeout de 5s — sin CT, GetHealthAsync dependía del KeepAlive TCP (~30s),
                        // bloqueando la generación del informe de diagnóstico en el UI thread.
                        using var diagHealthCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var health = await cli.GetHealthAsync(diagHealthCts.Token);
                        if (health != null)
                        {
                            summary.AppendLine($"Remote health for {remoteIp}:{remotePort}");
                            summary.AppendLine($"  connCurrent={health.ConnCurrent}/{health.ConnLimit}, perIp={health.PerIpLimit}, activeIps={health.ActiveIps}");
                            summary.AppendLine($"  pinFails={health.PinFailsTracked}, hashCache={health.HashCacheEntries}, rate={health.CommandRateLimit}/{health.CommandRateWindowSeconds}s tracked={health.CommandRateTracked}");
                        }
                    }
                    catch (Exception ex)
                    {
                        summary.AppendLine($"Remote health query failed: {ex.Message}");
                    }
                }

                File.WriteAllText(Path.Combine(tempDir, "summary.txt"), summary.ToString());

                void CopyIfExists(string srcPath, string targetName)
                {
                    try
                    {
                        if (!File.Exists(srcPath)) return;
                        try
                        {
                            File.Copy(srcPath, Path.Combine(tempDir, targetName), overwrite: true);
                        }
                        catch (IOException)
                        {
                            var dest = Path.Combine(tempDir, targetName);
                            using var fsIn = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var fsOut = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                            fsIn.CopyTo(fsOut);
                        }
                    }
                    catch (Exception ex) { Log.Warn("diag", "copy-file-failed", new { srcPath, error = ex.Message }); }
                }

                // S3: redactar PIN antes de copiar settings.json al ZIP de diagnóstico
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var raw = File.ReadAllText(SettingsPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(raw);
                        var redactKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pin", "requiredPin" };
                        var dict = doc.RootElement.EnumerateObject()
                            .Where(p => !redactKeys.Contains(p.Name))
                            .ToDictionary(p => p.Name, p => p.Value.GetRawText());
                        var redacted = "{" + string.Join(",", dict.Select(kv => $"\"{kv.Key}\":{kv.Value}")) + "}";
                        File.WriteAllText(Path.Combine(tempDir, "settings.json"), redacted);
                    }
                }
                catch (Exception ex) { Log.Warn("diag", "settings-redact-failed", new { error = ex.Message }); }

                // S3: queue.json contiene rutas locales, IPs y nombres de archivo — redactar FullPath
                void CopyRedactingPaths(string src, string destName)
                {
                    if (!File.Exists(src)) return;
                    try
                    {
                        var raw = File.ReadAllText(src);
                        // Redactar el campo FullPath (puede aparecer en arrays JSON de FileEntry)
                        var redacted = System.Text.RegularExpressions.Regex.Replace(
                            raw,
                            @"""FullPath""\s*:\s*""[^""]*""",
                            @"""FullPath"":""[redacted]""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        File.WriteAllText(Path.Combine(tempDir, destName), redacted);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("diag", "queue-redact-failed-fallback-copy", new { src, error = ex.Message });
                        CopyIfExists(src, destName);
                    } // fallback: copiar sin redactar
                }
                CopyRedactingPaths(QueuePath, "queue.json");
                CopyIfExists(Log.CurrentLogFile, Path.GetFileName(Log.CurrentLogFile));

                if (Directory.Exists(Log.Directory_))
                {
                    foreach (var logFile in Directory.EnumerateFiles(Log.Directory_, "lancopy-*.log")
                                 .OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                    {
                        CopyIfExists(logFile, Path.GetFileName(logFile));
                    }
                }

                if (File.Exists(zipPath)) File.Delete(zipPath);
                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);

                SetStatus(L.Format("st.diagExported", zipPath));
                            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch (Exception ex) { Log.Debug("diag", "cleanup-temp-dir-failed", new { tempDir, error = ex.Message }); }
            }
        }
        catch (Exception ex)
        {
            Log.Error("diag", "export-failed", new { error = ex.Message });
            SetStatus(L.Format("st.diagExportFailed", ex.Message));
        }
    }

    private void CmbLang_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cmb) return;
        var idx = cmb.SelectedIndex;
        if (idx < 0 || idx >= Loc.Available.Count) return;
        var code = Loc.Available[idx].Code;
        if (code == L.Current) return;
        L.SetLanguage(code);
        RefreshDynamicTranslations();
    }

    private void RefreshDynamicTranslations()
    {
        this.FlowDirection = L.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ApplyUiMode();
        UpdateConnectButton(isConnected: _connectButtonIsConnected, isBusy: _connectButtonIsBusy);
        UpdateLocalPath();
        UpdateRemotePath();
        UpdateServerModeBadge();
        UpdateBroadcastButton();

        var bw = this.FindControl<TextBlock>("txtBwValue");
        if (bw != null && _bandwidthLimitMbps <= 0) bw.Text = L["bw.unlimited"];
    }
    private void TxtPin_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var raw = ((TextBox)sender!).Text?.Trim() ?? "";
        // S1: limitar longitud del PIN local - Q2: usar MaxPinLength de clase (128) en lugar de constante local duplicada (64 != 128)
        var trimmed = raw.Length > MaxPinLength ? raw.Substring(0, MaxPinLength) : raw;
        // S3: filtrar chars BiDi y no-imprimibles del PIN — un PIN con U+202E (RLO) se muestra
        // visualmente diferente a cómo se autentica, confundiendo al usuario al reproducirlo
        var pin = new string(trimmed.Where(c =>
        {
            int v = (int)c;
            if (v < 0x20) return false;
            if (v >= 0xD800 && v <= 0xDFFF) return false; // surrogates
            if (c == (char)0x202E || c == (char)0x202D) return false; // RLO/LRO
            if (c == (char)0x200F || c == (char)0x200E) return false; // RLM/LRM
            if (c == (char)0x2066 || c == (char)0x2067 || c == (char)0x2068 || c == (char)0x2069) return false; // LRI/RLI/FSI/PDI
            if (c == (char)0xFEFF) return false; // BOM
            return true;
        }).ToArray());
        _server.RequiredPin = string.IsNullOrEmpty(pin) ? null : pin;
    }

    private LanClient MakeClient(string ip, int port, bool? useTlsOverride = null)
    {
        var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
        return new LanClient(ip, port)
        {
            Pin = string.IsNullOrEmpty(pin) ? null : pin,
            UseTls = ResolveClientTlsMode(ip, port, useTlsOverride),
            UseCompress = _compressEnabled
        };
    }

    private bool ResolveClientTlsMode(string ip, int port, bool? useTlsOverride)
    {
        if (useTlsOverride is bool explicitMode) return explicitMode;
        var discoveredPeer = _discovery?.GetPeers().FirstOrDefault(p => p.Ip == ip && p.Port == port);
        if (discoveredPeer?.TlsEnabled == true) return true;
        if (discoveredPeer?.TlsEnabled == false && _plaintextCompatibilityApprovedPeers.Contains(PeerSecurityKey(ip, port))) return false;
        return _tlsEnabled;
    }

    private static string PeerSecurityKey(string ip, int port) => $"{ip.Trim()}:{port}";

    // Ideas

    // idea-bwlimit: el slider fija el limite global de ancho de banda (0 = ilimitado).
    private void Bandwidth_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var mbps = Math.Clamp((int)Math.Round(e.NewValue), 0, 12);
        _bandwidthLimitMbps = mbps;
        RateLimiter.Global.BytesPerSecond = mbps <= 0 ? 0 : (long)mbps * 1024 * 1024;
        var lbl = this.FindControl<TextBlock>("txtBwValue");
        if (lbl != null) lbl.Text = mbps <= 0 ? L["bw.unlimited"] : $"{mbps} MB/s";
        SaveSettingsDeferred(
            this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "",
            this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742");
    }

    // Chat entre PCs en ventana independiente. Si no hay destino escrito, responde al último remitente.
    private void OpenChat_Click(object? sender, RoutedEventArgs e) => ShowChatWindow(activate: true);

    private void ShowChatWindow(bool activate)
    {
        if (_chatWindow != null)
        {
            if (activate)
            {
                _chatWindow.Activate();
            }

            return;
        }

        _chatWindow = new ChatWindow(_chatMessages, SendChatMessageAsync, focusInputOnOpen: activate);
        _chatWindow.Closed += (_, _) => _chatWindow = null;
        _chatWindow.Show(this);

        if (activate)
        {
            _chatWindow.Activate();
        }
    }

    private async Task<bool> SendChatMessageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { SetStatus(L["st.textEmpty"]); return false; }

        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = NetworkValidation.ParsePortOrDefault(this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim());
        if (string.IsNullOrEmpty(ip) && !string.IsNullOrWhiteSpace(_lastTextSenderIp))
        {
            ip = _lastTextSenderIp!;
            port = _lastTextSenderPort;
        }
        if (string.IsNullOrEmpty(ip)) { SetStatus(L["st.connectFirst"]); return false; }

        try
        {
            using var cli = MakeClient(ip, port);
            using var sendTextCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            var message = text.Trim();
            await cli.SendTextAsync(message, sendTextCts.Token);
            _chatMessages.Add(new ChatMessage { Sender = L["chat.me"], Text = message, IsOwn = true });
            SetStatus(L["st.textSent"]);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("st.textFailed", ex.Message));
            return false;
        }
    }

    private static string SingleLinePreview(string text, int maxChars = 60)
    {
        var singleLine = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return singleLine.Length > maxChars ? singleLine[..maxChars] + "\u2026" : singleLine;
    }
    // Chat: registrar el mensaje recibido y notificarlo.
    private void OnTextReceived(string ip, string text)
    {
        var peer = _discovery?.GetPeers().FirstOrDefault(p => string.Equals(p.Ip, ip, StringComparison.OrdinalIgnoreCase));
        _lastTextSenderIp = ip;
        _lastTextSenderPort = peer?.Port ?? NetworkValidation.DefaultPort;
        var senderName = !string.IsNullOrWhiteSpace(peer?.Name) ? peer!.Name : ip;
        Dispatcher.UIThread.Post(() =>
        {
            var preview = SingleLinePreview(text);
            _chatMessages.Add(new ChatMessage { Sender = senderName, Text = text, IsOwn = false });
            ShowChatWindow(activate: false);
            SetStatus(L.Format("st.textReceived", senderName, preview));

            // Si es un enlace HTTP/S y auto-open está activo, lo abrimos
            if (_safeModeEnabled)
                return;

            if (_autoOpenLinks && (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var ps = new ProcessStartInfo { FileName = text, UseShellExecute = true };
                    Process.Start(ps)?.Dispose();
                }
                catch (Exception ex) { Log.Warn("chat", "auto-open-link-failed", new { url = text, error = ex.Message }); }
            }
        });
    }

    // F7: Broadcast PARALELO a todos los peers descubiertos
    private async void Broadcast_Click(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        var peers = _discovery?.GetPeers() ?? [];
        if (peers.Count == 0) { SetStatus(L["st.noPeers"]); return; }
        SetStatus(L.Format("st.broadcastStarting", peers.Count));
        int okPeers = 0, failPeers = 0;
        var lk = new object();
        try
        {
        await Parallel.ForEachAsync(peers,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Min(peers.Count, 4) },
            async (peer, ct) =>
            {
                try
                {
                    using var cli = MakeClient(peer.Ip, peer.Port);
                    foreach (var it in items)
                        await cli.UploadAsync(it.FullPath, Path.GetFileName(it.FullPath), null, ct);
                    lock (lk) { okPeers++; }
                    SetStatus(L.Format("st.broadcastPeerOk", peer.Name));
                }
                catch (Exception ex) { lock (lk) { failPeers++; } Log.Warn("broadcast", "peer-failed", new { ip = peer.Ip, error = ex.Message }); }
            });
        SetStatus(L.Format("st.broadcastDone", okPeers, failPeers));
        }
        catch (OperationCanceledException) { SetStatus(L.Format("st.broadcastDone", okPeers, failPeers)); }
        catch (Exception ex) { Log.Warn("broadcast", "broadcast-click-unexpected", new { error = ex.Message }); }
    }

    // F7: Enviar a peers seleccionados por el usuario
    private async void SendToSelectedPeers_Click(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        var allPeers = _discovery?.GetPeers() ?? [];
        if (allPeers.Count == 0) { SetStatus(L["st.noPeers"]); return; }

        try
        {
        var checkBoxes = allPeers.Select(p => new CheckBox
        {
            Content = $"{p.Name}  ({p.Ip}:{p.Port})",
            IsChecked = true,
            Margin = new Thickness(0, 2)
        }).ToList();

        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = L["dlg.selectPeers"], FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        foreach (var cb in checkBoxes) panel.Children.Add(cb);
        var sendBtn = new Button
        {
            Content = L["btn.send"],
            Background = SolidColorBrush.Parse("#28A745"),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 6),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(sendBtn);

        bool proceed = false;
        var dlg = new Window
        {
            Title = L["dlg.sendTopeers"],
            Content = new ScrollViewer { Content = panel },
            Width = 380,
            Height = Math.Min(450, 130 + allPeers.Count * 34),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        sendBtn.Click += (_, _) => { proceed = true; dlg.Close(); };
        await dlg.ShowDialog(this);
        if (!proceed) return;

        var selected = allPeers.Where((_, i) => checkBoxes[i].IsChecked == true).ToList();
        if (selected.Count == 0) return;

        var lk2 = new object(); int ok2 = 0, fail2 = 0;
        await Parallel.ForEachAsync(selected,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Min(selected.Count, 4) },
            async (peer, ct) =>
            {
                try
                {
                    using var cli = MakeClient(peer.Ip, peer.Port);
                    foreach (var it in items)
                        await cli.UploadAsync(it.FullPath, Path.GetFileName(it.FullPath), null, ct);
                    lock (lk2) { ok2++; }
                }
                catch (Exception ex)
                {
                    lock (lk2) { fail2++; }
                    Log.Warn("broadcast", "selected-peer-failed", new { ip = peer.Ip, peer.Port, error = ex.Message });
                }
            });
        SetStatus(L.Format("st.broadcastDone", ok2, fail2));
        }
        catch (Exception ex)
        {
            Log.Warn("broadcast", "send-to-selected-peers-unexpected", new { error = ex.Message });
            SetStatus(L[ex.Message]);
        }
    }

    // idea-qr: mostrar un QR con el enlace de emparejamiento (ip+puerto+pin).
    private async void ShowQr_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim();
            var link = PairingLink.Build(_server.LocalIp, _server.Port, string.IsNullOrEmpty(pin) ? null : pin);
            Bitmap? bmp = null;
            try
            {
                using var gen = new QRCodeGenerator();
                using var data = gen.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
                var png = new PngByteQRCode(data).GetGraphic(8);
                using var ms = new MemoryStream(png);
                bmp = new Bitmap(ms);
            }
            catch (Exception ex)
            {
                Log.Debug("qr", "generate-qr-image-failed", new { error = ex.Message });
                bmp = null;
            }

            var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
            if (bmp != null) panel.Children.Add(new Image { Source = bmp, Width = 280, Height = 280 });
            panel.Children.Add(new TextBlock { Text = link, TextWrapping = TextWrapping.Wrap, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Center });
            var dlg = new Window { Title = L["qr.title"], Width = 360, Height = bmp != null ? 430 : 180, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
            await dlg.ShowDialog(this);
        }
        catch (Exception ex) { SetStatus(L.Format("st.qrFailed", ex.Message)); }
    }
    // idea-sendto: crear un acceso directo en la carpeta "Enviar a" de Windows.
    private void SendToIntegration_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows()) { SetStatus(L["st.sendtoUnsupported"]); return; }
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) { SetStatus(L["st.sendtoFailed"]); return; }
            var sendTo = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            var lnk = Path.Combine(sendTo, "LanCopy.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) { SetStatus(L["st.sendtoUnsupported"]); return; }
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic sc = shell.CreateShortcut(lnk);
            sc.TargetPath = exe;
            sc.WorkingDirectory = Path.GetDirectoryName(exe);
            sc.IconLocation = exe + ",0";
            sc.Description = "LanCopy";
            sc.Save();
            SetStatus(L["st.sendtoDone"]);
        }
        catch (Exception ex) { SetStatus(L.Format("st.sendtoFailed2", ex.Message)); }
    }

    // idea-tray: configurar el icono de bandeja con menu Mostrar/Salir.
    private void SetupTray()
    {
        try
        {
            var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://LanCopy/Assets/app.ico")));
            var menu = new NativeMenu();
            var show = new NativeMenuItem(L["tray.show"]);
            show.Click += (_, _) => ShowFromTray();
            var exit = new NativeMenuItem(L["tray.exit"]);
            exit.Click += (_, _) => Close();
            menu.Items.Add(show);
            menu.Items.Add(exit);
            _tray = new TrayIcon { Icon = icon, ToolTipText = "LanCopy", Menu = menu, IsVisible = true };
            _tray.Clicked += (_, _) => ShowFromTray();
        }
        catch (Exception ex)
        {
            Log.Warn("tray", "setup-tray-failed", new { error = ex.Message });
            _tray = null;
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Muestra notificación cuando una transferencia completa con la ventana oculta/minimizada.
    /// </summary>
    private void NotifyTransferComplete(bool isUpload, int fileCount, long bytes, TimeSpan elapsed)
    {
        // Solo notificar si la ventana no es visible (minimizada a tray)
        if (IsVisible && WindowState != WindowState.Minimized) return;

        try
        {
            var direction = isUpload ? L["notify.sent"] : L["notify.received"];
            var sizeStr = FileEntry.FormatSize(bytes);
            var timeStr = elapsed.TotalSeconds < 1 ? "<1s" : $"{elapsed.TotalSeconds:F0}s";
            var title = L.Format("notify.transferDone", direction);
            var body = fileCount == 1
                ? L.Format("notify.transferBody1", sizeStr, timeStr)
                : L.Format("notify.transferBodyN", fileCount, sizeStr, timeStr);

            Dispatcher.UIThread.Post(() =>
            {
                _notifManager?.Show(new Avalonia.Controls.Notifications.Notification(
                    title, body,
                    Avalonia.Controls.Notifications.NotificationType.Success,
                    TimeSpan.FromSeconds(8)));
            });

            // En Windows, intentar mostrar toast nativo del sistema para que aparezca
            // aunque la ventana esté oculta (el NotificationManager de Avalonia necesita ventana visible)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var escaped = body.Replace("\"", "`\"");
                    var ps = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-NoProfile -Command \"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null; $xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent(1); $texts = $xml.GetElementsByTagName('text'); $texts[0].AppendChild($xml.CreateTextNode('{title.Replace("'", "''")}')) > $null; $texts[1].AppendChild($xml.CreateTextNode('{escaped.Replace("'", "''")}')) > $null; [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('LanCopy').Show([Windows.UI.Notifications.ToastNotification]::new($xml))\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(ps)?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Debug("notify", "native-toast-failed", new { error = ex.Message });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("notify", "transfer-notification-failed", new { error = ex.Message });
        }
    }


    /// <summary>Sonido del sistema al completar transferencia.</summary>
    private static void PlayTransferSound()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Reproduce el sonido de notificación de Windows sin dependencias extra
                var ps = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"[System.Media.SystemSounds]::Asterisk.Play()\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(ps)?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Debug("ui", "play-sound-failed", new { error = ex.Message });
        }
    }

    // Paralelismo configurable
    private void Parallel_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var n = Math.Max(1, Math.Min(8, (int)Math.Round(e.NewValue)));
        if (n == _maxParallel) return;
        _maxParallel = n;
        bool semApplied = false;
        lock (_semLock)
        {
            if (_isTransferring == 0)
            {
                var old = _transferSemaphore;
                _transferSemaphore = new SemaphoreSlim(n, n);
                old?.Dispose();
                semApplied = true;
            }
        }
        if (semApplied)
        {
            // Q6: actualizar label solo cuando el semaforo fue realmente reemplazado
            var lbl = this.FindControl<TextBlock>("txtParallelValue");
            if (lbl != null) lbl.Text = n.ToString();
            SetStatus(L.Format("st.parallelChanged", n));
        }
        else
        {
            // Q6: durante transferencia: NO actualizar el label (mostraria valor incorrecto)
            // U1: st.parallelPendingTransfer cae como fallback al key si no est\u00e1 en JSON (aceptable)
            SetStatus(L.Format("st.parallelChanged", n)); // deferred \u2014 se aplicar\u00e1 al finalizar la transferencia
        }
    }


    // ————————————————————————————————————————————————————————————————————————

    private async void ShowAudit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Cargar ultimos 7 dias de auditoría
            var allRecords = new List<AuditService.AuditRecord>();
            for (int i = 0; i < 7; i++)
            {
                var day = DateTime.UtcNow.AddDays(-i).ToString("yyyyMMdd");
                var records = await AuditService.ReadDay(day);
                allRecords.AddRange(records);
            }
            allRecords = allRecords.OrderByDescending(r => r.Timestamp).ToList();

            // Estado del filtro
            var filteredRecords = allRecords.ToList();
            string currentOpFilter = "all";
            int currentDayFilter = 7; // 1=today, 2=2days, 7=week

            var bg = (IBrush?)Application.Current?.Resources["WindowBg"] ?? new SolidColorBrush(Color.Parse("#1E1E1E"));
            var headerBg = new SolidColorBrush(Color.Parse("#2A2A2E"));
            var borderBrush = new SolidColorBrush(Color.Parse("#3A3A3E"));
            var textFg = new SolidColorBrush(Color.Parse("#CCCCCC"));
            var mutedFg = new SolidColorBrush(Color.Parse("#888888"));
            var accentFg = new SolidColorBrush(Color.Parse("#4FC3F7"));
            var successFg = new SolidColorBrush(Color.Parse("#66BB6A"));
            var errorFg = new SolidColorBrush(Color.Parse("#EF5350"));

            // Panel de contenido (se reconstruye al filtrar)
            var contentStack = new StackPanel { Spacing = 1 };

            void RebuildContent()
            {
                var src = allRecords.AsEnumerable();
                if (currentDayFilter < 7)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-currentDayFilter);
                    src = src.Where(r => DateTime.TryParse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) && dt >= cutoff);
                }
                if (currentOpFilter != "all")
                    src = src.Where(r => r.Operation.Equals(currentOpFilter, StringComparison.OrdinalIgnoreCase));
                filteredRecords = src.ToList();

                contentStack.Children.Clear();
                var dateFmt = L["audit.dateFormat"];
                // O10: brushes pre-calculados fuera del loop — antes se creaban hasta 1000+ SolidColorBrush
                // (500 filas × 2-3 brushes) por rebuild al usar filtros. Color.Parse es string parsing = costoso.
                var rowBgEven  = new SolidColorBrush(Color.Parse("#252528"));
                var rowBgOdd   = new SolidColorBrush(Color.Parse("#1E1E22"));
                var syncFg     = new SolidColorBrush(Color.Parse("#FFA726"));
                int rowIndex = 0;
                foreach (var r in filteredRecords.Take(500))
                {
                    var ts = DateTime.TryParse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtParsed)
                        ? dtParsed.ToLocalTime().ToString(dateFmt) : r.Timestamp;
                    // O10: 0 allocs por fila (brushes reutilizados)
                    var rowBg = rowIndex % 2 == 0 ? rowBgEven : rowBgOdd;

                    var row = new Grid
                    {
                        ColumnDefinitions = ColumnDefinitions.Parse("24,140,70,60,*,80,60,80"),
                        Background = rowBg,
                        Margin = new Thickness(0, 0, 0, 1)
                    };

                    // Icono status
                    row.Children.Add(new TextBlock { Text = r.Success ? "\u2705" : "\u274C", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, [Grid.ColumnProperty] = 0 });
                    // Timestamp
                    row.Children.Add(new TextBlock { Text = ts, FontSize = 11, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 3), [Grid.ColumnProperty] = 1 });
                    // Operation
                    // O10: syncFg reutilizado (pre-calculado fuera del loop)
                    var opColor = r.Operation switch { "send" => accentFg, "receive" => successFg, "sync" => syncFg, _ => textFg };
                    row.Children.Add(new TextBlock { Text = r.Operation.ToUpper(), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = opColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 3), [Grid.ColumnProperty] = 2 });
                    // IP
                    row.Children.Add(new TextBlock { Text = r.Ip, FontSize = 10, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 3), TextTrimming = TextTrimming.CharacterEllipsis, [Grid.ColumnProperty] = 3 });
                    // Filename
                    var fnBlock = new TextBlock { Text = r.FileName, FontSize = 11, Foreground = textFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 3), TextTrimming = TextTrimming.CharacterEllipsis, [Grid.ColumnProperty] = 4 };
                    ToolTip.SetTip(fnBlock, r.FileName);
                    row.Children.Add(fnBlock);
                    // Size
                    row.Children.Add(new TextBlock { Text = r.Bytes > 0 ? FileEntry.FormatSize(r.Bytes) : "-", FontSize = 11, Foreground = textFg, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4, 3), [Grid.ColumnProperty] = 5 });
                    // Duration
                    var durText = r.DurationMs > 0 ? (r.DurationMs > 1000 ? $"{r.DurationMs / 1000.0:F1}s" : $"{r.DurationMs}ms") : "-";
                    row.Children.Add(new TextBlock { Text = durText, FontSize = 11, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4, 3), [Grid.ColumnProperty] = 6 });

                    // Error tooltip
                    if (r.Error != null && !r.Success) ToolTip.SetTip(row, $"\u26A0 {r.Error}");

                    // Botón Restaurar para DELETE local exitosos con info de papelera
                    if (r.Success && string.Equals(r.Operation, "delete", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(r.Error) && r.Error.StartsWith("trash:"))
                    {
                        var trashPath = r.Error.Substring(6); // longitud de "trash:"
                        if (File.Exists(trashPath) || Directory.Exists(trashPath))
                        {
                            var btnRestore = new Button
                            {
                                Content = "🔄",
                                Padding = new Thickness(6, 2),
                                FontSize = 10,
                                Margin = new Thickness(4, 0),
                                [Grid.ColumnProperty] = 7
                            };
                            ToolTip.SetTip(btnRestore, "Restore file from trash");
                            btnRestore.Click += async (_, _) =>
                            {
                                try
                                {
                                    if (File.Exists(r.FileName) || Directory.Exists(r.FileName))
                                    {
                                        if (!await MessageBox($"The file or folder '{Path.GetFileName(r.FileName)}' already exists at the destination. Do you want to overwrite it?", "Destination Exists"))
                                        {
                                            return;
                                        }
                                        if (Directory.Exists(r.FileName)) Directory.Delete(r.FileName, true);
                                        else File.Delete(r.FileName);
                                    }

                                    if (Directory.Exists(trashPath))
                                    {
                                        var targetDir = Path.GetDirectoryName(r.FileName);
                                        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                        Directory.Move(trashPath, r.FileName);
                                    }
                                    else if (File.Exists(trashPath))
                                    {
                                        var targetDir = Path.GetDirectoryName(r.FileName);
                                        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                                        File.Move(trashPath, r.FileName, overwrite: true); // TOCTOU-fix: atomic overwrite
                                    }
                                    btnRestore.IsEnabled = false;
                                    btnRestore.Content = "OK";
                                    SetStatus($"Archivo restaurado: {Path.GetFileName(r.FileName)}");
                                    _ = RefreshLocalAsync();
                                }
                                catch (Exception ex)
                                {
                                    await MessageBox($"Error al restaurar: {ex.Message}", "Restauración fallida");
                                }
                            };
                            row.Children.Add(btnRestore);
                        }
                    }

                    contentStack.Children.Add(row);
                    rowIndex++;
                }

                if (filteredRecords.Count == 0)
                    contentStack.Children.Add(new TextBlock { Text = L["audit.empty"], Foreground = mutedFg, FontSize = 13, Margin = new Thickness(16, 32), HorizontalAlignment = HorizontalAlignment.Center });
            }

            // Botones de filtro por dia
            var btnToday = new Button { Content = L["audit.today"], Padding = new Thickness(12, 4), FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            var btn2Days = new Button { Content = L["audit.twoDays"], Padding = new Thickness(12, 4), FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            var btnWeek = new Button { Content = L["audit.week"], Padding = new Thickness(12, 4), FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };

            btnToday.Click += (_, _) => { currentDayFilter = 1; RebuildContent(); };
            btn2Days.Click += (_, _) => { currentDayFilter = 2; RebuildContent(); };
            btnWeek.Click += (_, _) => { currentDayFilter = 7; RebuildContent(); };

            // Filtro por operacion
            var cmbOp = new ComboBox
            {
                ItemsSource = new[] { L["audit.allOps"], "SEND", "RECEIVE", "TEXT", "SYNC" },
                SelectedIndex = 0,
                FontSize = 11,
                MinWidth = 100,
                Margin = new Thickness(8, 0, 0, 0)
            };
            cmbOp.SelectionChanged += (_, _) =>
            {
                var opNames = new[] { "all", "send", "receive", "text", "sync" };
                currentOpFilter = cmbOp.SelectedIndex >= 0 && cmbOp.SelectedIndex < opNames.Length ? opNames[cmbOp.SelectedIndex] : "all";
                RebuildContent();
            };

            // Boton exportar CSV
            var btnExport = new Button { Content = "\uD83D\uDCBE CSV", Padding = new Thickness(12, 4), FontSize = 11, Margin = new Thickness(8, 0, 0, 0) };
            btnExport.Click += async (_, _) =>
            {
                try
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("Timestamp,Operation,IP,FileName,Bytes,DurationMs,Success,Error");
                    foreach (var r in filteredRecords)
                    {
                        var ts = r.Timestamp ?? "";
                        var op = r.Operation ?? "";
                        var ip = r.Ip ?? "";
                        var file = (r.FileName ?? "").Replace("\"", "\"\"");
                        var err = (r.Error ?? "").Replace("\"", "\"\"");
                        csv.AppendLine($"\"{ts}\",\"{op}\",\"{ip}\",\"{file}\",{r.Bytes},{r.DurationMs},{r.Success},\"{err}\"");
                    }
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"LanCopy_audit_{DateTime.Now:yyyyMMdd_HHmm}.csv");
                    await File.WriteAllTextAsync(path, csv.ToString());
                    SetStatus(L.Format("audit.exported", path));
                }
                catch (Exception ex) { SetStatus(L.Format("audit.exportFailed", ex.Message)); }
            };

            // Contador de registros
            var lblCount = new TextBlock { Text = L.Format("audit.header", allRecords.Count), Foreground = mutedFg, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };

            // Toolbar
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 8),
                Children = { btnToday, btn2Days, btnWeek, cmbOp, btnExport, lblCount }
            };

            // Header de columnas
            var header = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("24,140,70,60,*,80,60"),
                Background = headerBg,
                Margin = new Thickness(0, 0, 0, 2)
            };
            var colNames = new[] { "", L["audit.colTime"], L["audit.colOp"], "IP", L["audit.colFile"], L["audit.colSize"], L["audit.colDur"] };
            for (int c = 0; c < colNames.Length; c++)
            {
                header.Children.Add(new TextBlock
                {
                    Text = colNames[c], FontSize = 11, FontWeight = FontWeight.Bold,
                    Foreground = accentFg, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 6), [Grid.ColumnProperty] = c,
                    HorizontalAlignment = c >= 5 ? HorizontalAlignment.Right : HorizontalAlignment.Left
                });
            }

            RebuildContent();

            var scrollContent = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = contentStack
            };

            var mainPanel = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            DockPanel.SetDock(header, Dock.Top);
            mainPanel.Children.Add(toolbar);
            mainPanel.Children.Add(header);
            mainPanel.Children.Add(scrollContent);

            var dlg = new Window
            {
                Title = L["audit.title"],
                Width = 820, Height = 520,
                Content = mainPanel,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = bg
            };
            await dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("audit.errOpen", ex.Message));
        }
    }

    // -- F3: Modo Sync (enviar solo archivos modificados) ------------------------

    private async void SyncUpload_Click(object? sender, RoutedEventArgs e)
    {
        try { await SyncUploadInternalAsync(); }
        catch (Exception ex) { SetStatus(L.Format("st.error", ex.Message)); }
    }

    private async Task SyncUploadInternalAsync()
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        // B3: snapshot _client bajo lock para evitar race con reconexión concurrente
        LanClient? snapClient;
        await _clientLock.WaitAsync();
        try { snapClient = _client; } finally { _clientLock.Release(); }
        if (snapClient == null) { SetStatus(L["st.noConnection"]); return; }

        SetStatus(L.Format("st.syncStarting", items.Count));
        var toSend = new List<FileEntry>();
        var skipped = 0;

        // P2: un solo CTS compartido para todas las llamadas GetStatAsync (no alloc por archivo)
        using var statCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        int syncIdx = 0;
        foreach (var item in items)
        {
            syncIdx++;
            // U2: mostrar progreso durante las consultas stat
            SetStatus(L.Format("st.syncChecking", syncIdx, items.Count, item.Name));
            try
            {
                // P4: reusar statCts.Token con timeout global — evita alloc de CTS por archivo
                // El timeout por-archivo de 5s no es necesario: statCts ya tiene 30s globales
                var remotePath = string.IsNullOrEmpty(_remotePath)
                    ? item.Name
                    : $"{_remotePath}/{item.Name}";
                var stat = await snapClient.GetStatAsync(remotePath, statCts.Token); // P4: statCts reutilizado
                var localInfo = new FileInfo(item.FullPath);
                bool sameSize = stat != null && stat.Size == localInfo.Length;
                bool sameMtime = stat != null && Math.Abs(stat.LastWriteUtcTicks - localInfo.LastWriteTimeUtc.Ticks) < TimeSpan.TicksPerSecond;
                if (sameSize && sameMtime) { skipped++; continue; }
            }
            catch (Exception ex)
            {
                Log.Debug("sync", "stat-probe-failed-assume-changed", new { file = item.FullPath, error = ex.Message });
            }
            toSend.Add(item);
        }

        if (skipped > 0) SetStatus(L.Format("st.syncMode", skipped));
        if (toSend.Count == 0) { SetStatus(L.Format("st.syncMode", skipped)); return; }
        await TransferAsync(toSend, isUpload: true);
    }
    // F7: Toggle "pequeños primero"
    private void SmallFirst_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.CheckBox chk) return; // Q2: null guard
        _sortSmallestFirst = chk.IsChecked == true;
        SetStatus(_sortSmallestFirst ? L["st.smallFirstOn"] : L["st.smallFirstOff"]);
    }

    // U1: Toggle StealthMode — no emitir UDP broadcast (no apareces en red ajena)
    private void StealthMode_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.CheckBox chk) return;
        if (_discovery != null) _discovery.StealthMode = chk.IsChecked == true;
        SetStatus(chk.IsChecked == true ? L["st.stealthOn"] : L["st.stealthOff"]);
    }
    // U6: Indicador claro de seguridad visible en la barra superior.
    private void UpdateServerModeBadge()
    {
        var badge = this.FindControl<Avalonia.Controls.TextBlock>("txtServerModeBadge");
        if (badge == null) return;

        if (_safeModeEnabled)
        {
            badge.Text = L["security.safeBadge"];
            badge.Foreground = BrushConnected;
            ToolTip.SetTip(badge, L["security.safeSummary"]);
            badge.IsVisible = true;
            return;
        }

        if (_safeModeUntilClose)
        {
            badge.Text = L["security.moreAccessUntilCloseBadge"];
            badge.Foreground = BrushError;
            ToolTip.SetTip(badge, L["security.moreAccessUntilCloseSummary"]);
            badge.IsVisible = true;
            return;
        }

        if (_safeModeUntilUtc is not null)
        {
            var remaining = _safeModeUntilUtc.Value - DateTimeOffset.UtcNow;
            var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            badge.Text = L.Format("security.moreAccessRemainingBadge", minutes);
            badge.Foreground = BrushError;
            ToolTip.SetTip(badge, L.Format("security.moreAccessRemainingSummary", minutes));
            badge.IsVisible = true;
            return;
        }

        var risky = !_tlsEnabled
            || !_restrictShareRoot
            || !_safeModeNoRemoteDelete
            || !_requireHighRiskApproval
            || _remotePowerEnabled
            || _autoOpenLinks;

        badge.Text = risky ? L["security.advancedBadge"] : L["security.customBadge"];
        badge.Foreground = risky ? BrushError : BrushConnecting;
        ToolTip.SetTip(badge, risky ? L["security.advancedSummary"] : L["security.customSummary"]);
        badge.IsVisible = true;
    }
    // v5-F7: Mostrar/ocultar botón de broadcast según peers disponibles
    private void UpdateBroadcastButton()
    {
        var peers = _discovery?.GetPeers() ?? [];
        var btn = this.FindControl<Avalonia.Controls.Button>("btnBroadcast");
        if (btn == null) return;
        Dispatcher.UIThread.Post(() =>
        {
            btn.IsVisible = peers.Count > 0;
            ToolTip.SetTip(btn, L.Format("tip.broadcast", peers.Count));
        });
    }

    // v5-F7: Broadcast mejorado con feedback por peer y progreso
    private async void BroadcastDetailed_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        var peers = _discovery?.GetPeers() ?? [];
        if (peers.Count == 0) { SetStatus(L["st.noPeers"]); return; }

        SetStatus(L.Format("st.broadcastStarting", peers.Count));
        int okPeers = 0, failPeers = 0;
        var lk = new object();
        // U1: enlazar el CTS del broadcast al _uploadCts (botón Cancel) + timeout 2min por peer
        // BUG-FIX-B1: _uploadCts puede estar disposed si no hay upload activo - usar Token con guard
        using var broadcastTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2));
        System.Threading.CancellationToken uploadToken;
        try { uploadToken = _uploadCts.Token; }
        catch (ObjectDisposedException) { uploadToken = System.Threading.CancellationToken.None; }
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
            uploadToken, broadcastTimeout.Token);

        try
        {
        await System.Threading.Tasks.Parallel.ForEachAsync(peers,
            new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Min(peers.Count, 4), CancellationToken = cts.Token },
            async (peer, ct) =>
            {
                try
                {
                    using var cli = MakeClient(peer.Ip, peer.Port);
                    foreach (var it in items)
                        await cli.UploadAsync(it.FullPath, System.IO.Path.GetFileName(it.FullPath), null, ct);
                    lock (lk) { okPeers++; }
                    SetStatus(L.Format("st.broadcastPeerOk", peer.Name));
                }
                catch (Exception bEx)
                {
                    lock (lk) { failPeers++; }
                    Log.Warn("broadcast", "peer-fail", new { peer = peer.Name, error = bEx.Message });
                    SetStatus(L.Format("st.broadcastPeerFail", peer.Name));
                }
            });
        SetStatus(L.Format("st.broadcastDone", okPeers, failPeers));
        }
        catch (OperationCanceledException) { SetStatus(L.Format("st.broadcastDone", okPeers, failPeers)); }
        catch (Exception ex) { Log.Warn("broadcast", "broadcast-detailed-unexpected", new { error = ex.Message }); }
    }
    private void LocalFavorites_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn == null) return;

        var menu = new ContextMenu();
        var itemDocs = new MenuItem { Header = L["menu.remoteDocuments"] };
        itemDocs.Click += (_, _) => { NavigateLocal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); };
        var itemDownloads = new MenuItem { Header = L["menu.remoteDownloads"] };
        
        // Carpeta Descargas por defecto
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        itemDownloads.Click += (_, _) => { NavigateLocal(downloadsPath); };
        
        var itemDesktop = new MenuItem { Header = L["menu.remoteDesktop"] };
        itemDesktop.Click += (_, _) => { NavigateLocal(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); };

        menu.ItemsSource = new List<MenuItem> { itemDocs, itemDownloads, itemDesktop };
        menu.Open(btn);
    }

    private void NavigateLocal(string path)
    {
        if (Directory.Exists(path))
        {
            _localPath = path;
            _ = RefreshLocalAsync();
            var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
            var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
            SaveSettings(ip, port);
        }
    }

    private async Task NavigateRemoteAsync(string path)
    {
        if (_client == null) { SetStatus(L["st.notConnected"]); return; }
        _remotePath = path;
        try { await RefreshRemoteAsync(); }
        catch (Exception ex) { Log.Warn("browser", "remote-favorites-nav-failed", new { error = ex.Message }); }
    }

    private void RemoteFavorites_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn == null) return;

        var menu = new ContextMenu();
        var itemHome = new MenuItem { Header = L["menu.remoteDrives"] };
        itemHome.Click += (_, _) => { _ = NavigateRemoteAsync(""); };
        
        var itemDocs = new MenuItem { Header = L["menu.remoteDocuments"] };
        itemDocs.Click += (_, _) => { _ = NavigateRemoteAsync("Documents"); };
        
        var itemDownloads = new MenuItem { Header = L["menu.remoteDownloads"] };
        itemDownloads.Click += (_, _) => { _ = NavigateRemoteAsync("Downloads"); };
        
        var itemDesktop = new MenuItem { Header = L["menu.remoteDesktop"] };
        itemDesktop.Click += (_, _) => { _ = NavigateRemoteAsync("Desktop"); };

        menu.ItemsSource = new List<MenuItem> { itemHome, itemDocs, itemDownloads, itemDesktop };
        menu.Open(btn);
    }

    private void RemotePower_Click(object? sender, RoutedEventArgs e)
    {
        if (_safeModeEnabled)
        {
            SetStatus(L["security.blocksPower"]);
            return;
        }
        var btn = sender as Button;
        if (btn == null) return;

        var menu = new ContextMenu();
        var itemReboot = new MenuItem { Header = "🔄 Restart Remote Computer" };
        itemReboot.Click += async (_, _) => { await ConfirmAndExecutePowerAction("reboot", "Restart the remote computer?"); };
        
        var itemShutdown = new MenuItem { Header = "🛑 Shut Down Remote Computer" };
        itemShutdown.Click += async (_, _) => { await ConfirmAndExecutePowerAction("shutdown", "Shut down the remote computer?"); };

        menu.ItemsSource = new List<MenuItem> { itemReboot, itemShutdown };
        menu.Open(btn);
    }

    private async Task ConfirmAndExecutePowerAction(string action, string message)
    {
        if (_safeModeEnabled)
        {
            SetStatus(L["security.blocksPower"]);
            return;
        }
        if (_client == null) { SetStatus(L["st.notConnected"]); return; }
        if (!await MessageBox(message, "Confirm Power Action")) return;

        // BUG-FIX: snapshot bajo _clientLock — el MessageBox puede tardar segundos y _client
        // puede haberse nullado por desconexión/watchdog en ese intervalo.
        LanClient? snap;
        await _clientLock.WaitAsync();
        try { snap = _client; }
        finally { _clientLock.Release(); }
        if (snap == null) { SetStatus(L["st.notConnected"]); return; }

        try
        {
            SetStatus($"Sending {action} command to remote PC...");
            await snap.SendPowerAsync(action);
            SetStatus($"{action.ToUpper()} command executed on remote PC.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error sending power command: {ex.Message}");
            Log.Warn("power", "remote-power-failed", new { action, error = ex.Message });
        }
    }


    private DispatcherTimer? _searchDebounceTimer;

    private void TxtRemoteSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        if (_client == null)
        {
            SetStatus(L["st.notConnected"]);
            return;
        }

        // Cancelar y reusar el timer de debounce — no crear uno nuevo por keystroke
        // (el patron anterior creaba un DispatcherTimer nuevo sin desuscribir el handler anterior, leak)
        if (_searchDebounceTimer == null)
        {
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                var tb = this.FindControl<TextBox>("txtRemoteSearch");
                var query = tb?.Text?.Trim() ?? "";

                // BUG-FIX: snapshot del client bajo _clientLock — no usar _client directamente.
                // El Tick ocurre 500ms después del TextChanged: _client puede haberse nullado
                // por otro hilo (watchdog/reconexión) en ese intervalo → NullReferenceException.
                LanClient? snap;
                await _clientLock.WaitAsync();
                try { snap = _client; }
                finally { _clientLock.Release(); }
                if (snap == null) return;

                if (string.IsNullOrEmpty(query))
                {
                    try { await RefreshRemoteAsync(); }
                    catch { }
                    return;
                }
                try
                {
                    SetStatus($"Searching '{query}' on remote PC...");
                    var results = await snap.SearchRemoteAsync(_remotePath, query);
                    _remoteItemsAll = results;
                    ApplyRemoteSort();
                    SetStatus($"Remote search completed: {results.Count} results.");
                }
                catch (Exception ex)
                {
                    SetStatus($"Remote search failed: {ex.Message}");
                }
            };
        }
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }


    private async Task ProcessStartupArgsAsync(string[] args)
    {
        await Task.Delay(1500);

        try
        {
            var files = new List<FileEntry>();
            foreach (var path in args)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        files.Add(new FileEntry { Name = fi.Name, FullPath = fi.FullName, Size = fi.Length });
                    }
                    else if (Directory.Exists(path))
                    {
                        var di = new DirectoryInfo(path);
                        files.Add(new FileEntry { Name = di.Name, FullPath = di.FullName, Size = 0, IsDirectory = true });
                    }
                }
                catch { }
            }

            if (files.Count > 0)
            {
                if (await IsConnectedAsync())
                {
                    SetStatus($"Sending {files.Count} file(s) received from parameters...");
                    await TransferAsync(files, isUpload: true);
                }
                else
                {
                    var first = args[0];
                    try
                    {
                        var dir = Path.GetDirectoryName(first);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            _localPath = dir;
                            await RefreshLocalAsync();
                        }
                    }
                    catch { }
                    SetStatus("Files received. Connect to a remote PC to send them.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("startup", "process-startup-args-failed", new { error = ex.Message });
        }
    }
}
