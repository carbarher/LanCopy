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
    // ══ UX para usuarios sin conocimientos de red ════════════════════════════

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

    // Diagnostico "no veo el otro PC": comprueba red y equipos detectados y lo
    // explica en lenguaje llano, sin tecnicismos.
    private void Diagnose_Click(object? sender, RoutedEventArgs e)
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
                foreach (var p in peers) sb.AppendLine($"  • {p.Name}  ({p.Ip}:{p.Port})");
        }

        sb.AppendLine();
        sb.AppendLine(L["diag.tips"]);

        try { new InfoDialog(L["diag.title"], sb.ToString()).ShowDialog(this); } catch { }
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
    }
    private void TxtPin_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var pin = ((TextBox)sender!).Text?.Trim() ?? "";
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

    // ══ Ideas locas ══════════════════════════════════════════════════════════

    // idea-bwlimit: el slider fija el limite global de ancho de banda (0 = ilimitado).
    private void Bandwidth_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var mbps = (int)Math.Round(e.NewValue);
        RateLimiter.Global.BytesPerSecond = mbps <= 0 ? 0 : (long)mbps * 1024 * 1024;
        var lbl = this.FindControl<TextBlock>("txtBwValue");
        if (lbl != null) lbl.Text = mbps <= 0 ? L["bw.unlimited"] : $"{mbps} MB/s";
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
            AddHistory(L.Format("hist.textReceived", ip), "#00BCD4");
        });
    }

    // idea-broadcast: enviar los ficheros seleccionados a todos los peers descubiertos.
    private async void Broadcast_Click(object? sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems("localList").Where(f => f.Name != ".." && !f.IsDirectory).ToList();
        if (items.Count == 0) { SetStatus(L["st.selectFiles"]); return; }
        var peers = _discovery?.GetPeers() ?? [];
        if (peers.Count == 0) { SetStatus(L["st.noPeers"]); return; }
        int okPeers = 0, failPeers = 0;
        foreach (var peer in peers)
        {
            try
            {
                using var cli = MakeClient(peer.Ip, peer.Port);
                foreach (var it in items)
                    await cli.UploadAsync(it.FullPath, Path.GetFileName(it.FullPath));
                okPeers++;
                SetStatus(L.Format("st.broadcastPeerOk", peer.Name));
            }
            catch (Exception ex) { failPeers++; Debug.WriteLine($"[broadcast] {peer.Ip}: {ex.Message}"); }
        }
        SetStatus(L.Format("st.broadcastDone", okPeers, failPeers));
        AddHistory(L.Format("hist.broadcast", okPeers), okPeers > 0 ? "#28A745" : "#FF6B6B");
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
            exit.Click += (_, _) => { _reallyExit = true; Close(); };
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

}
