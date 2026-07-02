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
    // â•â• UX para usuarios sin conocimientos de red â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
        try { SaveSettings(this.FindControl<TextBox>("txtRemoteIp")?.Text ?? "", this.FindControl<TextBox>("txtRemotePort")?.Text ?? "8742"); } catch { }
    }

    // Abre el asistente de bienvenida (tambien accesible con el boton de ayuda).
    private async void ShowWelcome_Click(object? sender, RoutedEventArgs e)
    {
        try { await new WelcomeDialog().ShowDialog(this); } catch { }
    }

    // Q3: helper extraído; Q1/B7: usar HealthInfo? en vez de dynamic? para type safety
    private async Task<LanCopy.Services.LanClient.RemoteHealth?> TryGetRemoteHealthAsync(string remoteIp, int remotePort)
    {
        try
        {
            using var cli = MakeClient(remoteIp, remotePort);
            return await cli.GetHealthAsync();
        }
        catch { return null; }
    }

        // Diagnostico "no veo el otro PC": comprueba red y equipos detectados y lo
    // explica en lenguaje llano, sin tecnicismos.
    private async void Diagnose_Click(object? sender, RoutedEventArgs e)
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
                foreach (var p in peers) sb.AppendLine($"  â€¢ {p.Name}  ({p.Ip}:{p.Port})");
        }

        var remoteIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var remotePortText = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        if (!string.IsNullOrWhiteSpace(remoteIp) && int.TryParse(remotePortText, out var remotePort))
        {
            // Q6: usar el helper extraído en lugar de código inline duplicado
            var health = await TryGetRemoteHealthAsync(remoteIp, remotePort);
            if (health != null)
            {
                sb.AppendLine();
                sb.AppendLine(L["diag.remoteHealth"]); // U1: era hardcodeado en español
                sb.AppendLine(L.Format("diag.connActive", health.ConnCurrent, health.ConnLimit));
                sb.AppendLine(L.Format("diag.perIpLimit", health.PerIpLimit, health.ActiveIps));
                sb.AppendLine(L.Format("diag.pinFails", health.PinFailsTracked));
                sb.AppendLine(L.Format("diag.hashCache", health.HashCacheEntries));
                sb.AppendLine(L.Format("diag.rateLimit", health.CommandRateLimit, health.CommandRateWindowSeconds, health.CommandRateTracked));
            }
        }

        try { await new InfoDialog(L["diag.title"], sb.ToString()).ShowDialog(this); } catch { }
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
                summary.AppendLine($"SafeModeNoDelete: {_safeModeNoRemoteDelete}; ReadOnly: {_readOnly}; RequireApproval: {_requireApproval}");
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
                        var health = await cli.GetHealthAsync();
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
                        if (File.Exists(srcPath))
                            File.Copy(srcPath, Path.Combine(tempDir, targetName), overwrite: true);
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
                    catch { CopyIfExists(src, destName); } // fallback: copiar sin redactar
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

                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                    await top.Clipboard.SetTextAsync(zipPath);

                SetStatus(L.Format("st.diagExported", zipPath));
                            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
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
        this.FlowDirection = L.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        UpdateConnectButton(isConnected: _connectButtonIsConnected, isBusy: _connectButtonIsBusy);
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

    private LanClient MakeClient(string ip, int port)
    {
        var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
        return new LanClient(ip, port)
        {
            Pin = string.IsNullOrEmpty(pin) ? null : pin,
            UseTls = _tlsEnabled,
            UseCompress = _compressEnabled
        };
    }

    // â•â• Ideas locas â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

    // idea-clipboard: enviar un texto corto al remoto (se copia a su portapapeles).
    private async void SendText_Click(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("txtSendText");
        var text = box?.Text ?? "";
        if (string.IsNullOrEmpty(text)) { SetStatus(L["st.textEmpty"]); return; }
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        if (!int.TryParse(this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim(), out var port)) port = 8742;
        if (string.IsNullOrEmpty(ip)) { SetStatus(L["st.connectFirst"]); return; }
        try
        {
            using var cli = MakeClient(ip, port);
            await cli.SendTextAsync(text);
            if (box != null) box.Text = "";
            SetStatus(L["st.textSent"]);
        }
        catch (Exception ex) { SetStatus(L.Format("st.textFailed", ex.Message)); }
    }

    // idea-clipboard: texto recibido -> copiar al portapapeles y notificar.
    private void OnTextReceived(string ip, string text)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(text);
            }
            catch { }
            var preview = text.Length > 60 ? text[..60] + "\u2026" : text;
            SetStatus(L.Format("st.textReceived", ip, preview));
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

    // F7: Enviar a peers seleccionados por el usuario
    private async void SendToSelectedPeers_Click(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        var allPeers = _discovery?.GetPeers() ?? [];
        if (allPeers.Count == 0) { SetStatus(L["st.noPeers"]); return; }

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
                catch { lock (lk2) { fail2++; } }
            });
        SetStatus(L.Format("st.broadcastDone", ok2, fail2));
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
            catch { bmp = null; }

            var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
            if (bmp != null) panel.Children.Add(new Image { Source = bmp, Width = 280, Height = 280 });
            panel.Children.Add(new TextBlock { Text = link, TextWrapping = TextWrapping.Wrap, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Center });
            var copyBtn = new Button { Content = L["btn.copylink"], HorizontalAlignment = HorizontalAlignment.Center };
            copyBtn.Click += async (_, _) => { var t = TopLevel.GetTopLevel(this); if (t?.Clipboard != null) await t.Clipboard.SetTextAsync(link); };
            panel.Children.Add(copyBtn);
            var dlg = new Window { Title = L["qr.title"], Width = 360, Height = bmp != null ? 430 : 180, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
            await dlg.ShowDialog(this);
        }
        catch (Exception ex) { SetStatus(L.Format("st.qrFailed", ex.Message)); }
    }

    // idea-qr: pegar un enlace lancopy:// (o ip:puerto) y rellenar la conexion.
    private async void PasteLink_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            var text = top?.Clipboard != null ? await top.Clipboard.TryGetTextAsync() : null;
            var parsed = PairingLink.TryParse(text);
            if (parsed is not { } p) { SetStatus(L["st.linkInvalid"]); return; }
            this.FindControl<TextBox>("txtRemoteIp")!.Text = p.Ip;
            this.FindControl<TextBox>("txtRemotePort")!.Text = p.Port.ToString();
            if (p.Pin != null) this.FindControl<TextBox>("txtPin")!.Text = p.Pin;
            SetStatus(L.Format("st.linkParsed", $"{p.Ip}:{p.Port}"));
        }
        catch (Exception ex) { SetStatus(L.Format("st.linkFailed", ex.Message)); }
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
        catch { _tray = null; }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }


    // â”€â”€ Paralelismo configurable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            var today = await AuditService.ReadDay();
            var yesterday = await AuditService.ReadDay(DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd"));
            var records = today.Concat(yesterday).OrderByDescending(r => r.Timestamp).Take(200).ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(L.Format("audit.header", records.Count));
            sb.AppendLine();

            if (records.Count == 0)
            {
                sb.AppendLine(L["audit.empty"]);
            }
            else
            {
                var dateFmt = L["audit.dateFormat"];
                foreach (var r in records)
                {
                    var ts = DateTime.TryParse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtParsed)
                        ? dtParsed.ToLocalTime().ToString(dateFmt) : r.Timestamp;
                    var icon = r.Success ? "\u2705" : "\u274C";
                    var op = r.Operation.ToUpper();
                    var size = r.Bytes > 0 ? $" ({FileEntry.FormatSize(r.Bytes)})" : "";
                    var dur = r.DurationMs > 0 ? $" {r.DurationMs}ms" : "";
                    var err = r.Error != null ? $" \u26A0 {r.Error}" : "";
                    sb.AppendLine($"{icon} {ts}  [{op}]  {r.Ip}  {r.FileName}{size}{dur}{err}");
                }
            }

            var scrollContent = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = sb.ToString(),
                    FontFamily = new Avalonia.Media.FontFamily("Consolas,Courier New,monospace"),
                    FontSize = 12,
                    Margin = new Avalonia.Thickness(16),
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
                    TextWrapping = Avalonia.Media.TextWrapping.NoWrap
                }
            };

            var dlg = new Window
            {
                Title = L["audit.title"],
                Width = 720, Height = 480,
                Content = scrollContent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Avalonia.Media.IBrush?)Application.Current?.Resources["WindowBg"] ?? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"))
            };
            await dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus(L.Format("audit.errOpen", ex.Message)); // U1: era hardcodeado en español
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
            catch { /* si GetStat falla, asumir diferente ? incluir */ }
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

    // F3: Doble-click en texto recibido — copiarlo al portapapeles
    private async void ReceivedText_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var lb = sender as Avalonia.Controls.ListBox;
        if (lb?.SelectedItem is not string text) return;
        try
        {
            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(text);
            SetStatus(L["st.copied"]);
        }
        catch { }
    }

    // F4: Enviar contenido del portapapeles al peer conectado
    private async void SendClipboard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
            var text = top?.Clipboard != null ? await top.Clipboard.TryGetTextAsync() : null;
            text ??= "";
            if (string.IsNullOrEmpty(text)) { SetStatus(L["st.clipboardEmpty"]); return; }
            LanClient? snapClient;
            await _clientLock.WaitAsync();
            try { snapClient = _client; } finally { _clientLock.Release(); }
            if (snapClient == null) { SetStatus(L["st.noConnection"]); return; }
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            await snapClient.SendTextAsync(text, cts.Token);
            SetStatus(L.Format("st.clipboardSent", text.Length));
        }
        catch (Exception ex) { SetStatus(L.Format("st.error", ex.Message)); }
    }

    // U6: Actualizar badge de modo del servidor (ReadOnly, SafeMode, etc.)
    private void UpdateServerModeBadge()
    {
        var badge = this.FindControl<Avalonia.Controls.TextBlock>("txtServerModeBadge");
        if (badge == null) return;
        var parts = new List<string>();
        if (_readOnly) parts.Add($"\uD83D\uDD12 {L["badge.readOnly"]}"); // U3: emoji con escape unicode (era garbled '??')
        if (_safeModeNoRemoteDelete) parts.Add($"\uD83D\uDEE1 {L["badge.safeMode"]}"); // U3: idem
        badge.Text = parts.Count > 0 ? string.Join(" | ", parts) : "";
        badge.IsVisible = parts.Count > 0;
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
        // U1: enlazar el CTS del broadcast al _uploadCts (botón Cancel del usuario) + timeout global de 2min por peer
        using var broadcastTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
            _uploadCts.Token, broadcastTimeout.Token);

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
                catch
                {
                    lock (lk) { failPeers++; }
                    SetStatus(L.Format("st.broadcastPeerFail", peer.Name));
                }
            });

        SetStatus(L.Format("st.broadcastDone", okPeers, failPeers));
    }
}
