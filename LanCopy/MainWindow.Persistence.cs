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
    private void AddHistory(string text, string color,
        string operation = "", string peerIp = "", long bytes = 0, bool success = true)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _history.Insert(0, new TransferRecord
            {
                Time      = DateTime.Now.ToString("HH:mm:ss"),
                Text      = text,
                Color     = color,
                Operation = operation,
                PeerIp    = peerIp,
                Bytes     = bytes,
                Success   = success
            });
            while (_history.Count > 50) _history.RemoveAt(_history.Count - 1);

            // Auto-expand historial al primer item (#21)
            if (_historyExpander != null && !_historyExpander.IsExpanded)
                _historyExpander.IsExpanded = true;

            // Feature 7: toast notificación al completar
            if (_notifManager != null && color == "#28A745")
                _notifManager.Show(new Notification("LanCopy", text, NotificationType.Success));

            SaveHistory();
        });
    }

    private void SaveHistory() => HistoryStore.Save(HistoryPath, _history);

    private void LoadHistory()
    {
        var items = HistoryStore.Load(HistoryPath);
        if (items.Count == 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            _history.Clear();
            foreach (var it in items) _history.Add(it);
            if (_historyExpander != null && _history.Count > 0)
                _historyExpander.IsExpanded = true;
        });
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private async Task LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = await File.ReadAllTextAsync(SettingsPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (doc.TryGetProperty("remoteIp", out var ip)) this.FindControl<TextBox>("txtRemoteIp")!.Text = ip.GetString();
                if (doc.TryGetProperty("remotePort", out var port)) this.FindControl<TextBox>("txtRemotePort")!.Text = port.GetString();
                if (doc.TryGetProperty("localPort", out var lport))
                {
                    var lpTxt = lport.ValueKind == JsonValueKind.Number ? lport.GetInt32().ToString() : lport.GetString();
                    var lpc = this.FindControl<TextBox>("txtLocalPort"); if (lpc != null && !string.IsNullOrEmpty(lpTxt)) lpc.Text = lpTxt;
                }
            });
            if (doc.TryGetProperty("pin", out var pin))
            {
                var pinVal = pin.GetString() ?? "";
                await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBox>("txtPin")!.Text = pinVal);
                _server.RequiredPin = string.IsNullOrEmpty(pinVal) ? null : pinVal;
            }
            // Feature 9: TLS
            if (doc.TryGetProperty("tlsEnabled", out var tls))
            {
                _tlsEnabled = tls.GetBoolean();
                _server.TlsEnabled = _tlsEnabled;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkTls");
                    if (chk != null) chk.IsChecked = _tlsEnabled;
                });
            }
            // SEGURIDAD: confinamiento a carpeta compartida
            if (doc.TryGetProperty("restrictShareRoot", out var rsr))
            {
                _restrictShareRoot = rsr.GetBoolean();
                _server.RestrictToShareRoot = _restrictShareRoot;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkShareRoot");
                    if (chk != null) chk.IsChecked = _restrictShareRoot;
                });
            }
            // SEGURIDAD: modo solo lectura
            if (doc.TryGetProperty("readOnly", out var roEl))
            {
                _readOnly = roEl.GetBoolean();
                _server.ReadOnly = _readOnly;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkReadOnly");
                    if (chk != null) chk.IsChecked = _readOnly;
                });
            }
            // SEGURIDAD: consentimiento del receptor
            if (doc.TryGetProperty("requireApproval", out var raEl))
            {
                _requireApproval = raEl.GetBoolean();
                _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkRequireApproval");
                    if (chk != null) chk.IsChecked = _requireApproval;
                });
            }
            // Feature 2: compresión
            if (doc.TryGetProperty("compressEnabled", out var comp))
            {
                _compressEnabled = comp.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkCompress");
                    if (chk != null) chk.IsChecked = _compressEnabled;
                });
            }
            if (doc.TryGetProperty("telemetryEnabled", out var telemEnabled))
                _telemetryEnabled = telemEnabled.GetBoolean();
            if (doc.TryGetProperty("telemetryPromptShown", out var telemPrompt))
                _telemetryPromptShown = telemPrompt.GetBoolean();
            if (doc.TryGetProperty("telemetryInstallId", out var telemId))
                _telemetryInstallId = telemId.GetString() ?? "";
            if (doc.TryGetProperty("telemetryEndpoint", out var telemEp))
                _telemetryEndpoint = telemEp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(_telemetryInstallId))
                _telemetryInstallId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(_telemetryEndpoint))
                _telemetryEndpoint = Environment.GetEnvironmentVariable("LANCOPY_TELEMETRY_ENDPOINT") ?? "";
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var chk = this.FindControl<CheckBox>("chkTelemetry");
                if (chk != null) chk.IsChecked = _telemetryEnabled;
            });
            // UX: modo simple/avanzado y asistente de bienvenida
            if (doc.TryGetProperty("advancedMode", out var advEl))
            {
                _advancedMode = advEl.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(ApplyUiMode);
            }
            if (doc.TryGetProperty("welcomeShown", out var welEl))
                _welcomeShown = welEl.GetBoolean();
            // Tema UI
            if (doc.TryGetProperty("theme", out var themeEl))
            {
                _theme = themeEl.GetString() ?? "Dark";
                await Dispatcher.UIThread.InvokeAsync(ApplyTheme);
            }
            // Geometria de ventana persistente
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (doc.TryGetProperty("winW", out var wEl) && doc.TryGetProperty("winH", out var hEl))
                    {
                        var w = wEl.GetDouble(); var h = hEl.GetDouble();
                        if (w >= MinWidth && h >= MinHeight && w < 10000 && h < 10000) { Width = w; Height = h; }
                    }
                    if (doc.TryGetProperty("winX", out var xEl) && doc.TryGetProperty("winY", out var yEl))
                    {
                        var x = xEl.GetInt32(); var y = yEl.GetInt32();
                        if (x > -32000 && y > -32000 && x < 32000 && y < 32000) Position = new PixelPoint(x, y);
                    }
                    if (doc.TryGetProperty("winMax", out var mEl) && mEl.GetBoolean())
                        WindowState = WindowState.Maximized;
                }
                catch { }
            });
            // Feature 3: perfiles
            if (doc.TryGetProperty("profiles", out var profilesEl))
            {
                _profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(profilesEl.GetRawText()) ?? new();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Al iniciar, selecciona el perfil que coincida con la conexion restaurada.
                    var curIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                    var curPort = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "";
                    var match = _profiles.FirstOrDefault(p => p.Ip == curIp && p.Port == curPort)?.Name;
                    RefreshProfilesCombo(match);
                });
            }
        }
        catch { }
    }

    private void SaveSettings(string ip, string port)
    {
        try
        {
            var pin = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var __json = JsonSerializer.Serialize(new
            {
                remoteIp = ip,
                remotePort = port,
                localPort = this.FindControl<TextBox>("txtLocalPort")?.Text?.Trim() ?? "8742",
                pin,
                tlsEnabled = _tlsEnabled,
                restrictShareRoot = _restrictShareRoot,
                readOnly = _readOnly,
                requireApproval = _requireApproval,
                compressEnabled = _compressEnabled,

                advancedMode = _advancedMode,
                welcomeShown = _welcomeShown,
                language = L.Current,
                theme = _theme,
                winW = this.Width,
                winH = this.Height,
                winX = this.Position.X,
                winY = this.Position.Y,
                winMax = this.WindowState == WindowState.Maximized,
                profiles = _profiles
            });
            // Escritura atomica centralizada (temp + replace).
            JsonStore.WriteRawAtomic(SettingsPath, __json);
        }
        catch { }
    }

    private void ConfigureTelemetry()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        TelemetryService.Configure(_telemetryEnabled, _telemetryEndpoint, _telemetryInstallId, version);
    }

}




