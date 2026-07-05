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
                // Restaurar último peer descubierto en cmbPeers antes de que arranque la discovery
                if (doc.TryGetProperty("lastPeer", out var lpeerEl))
                {
                    var lastPeer = lpeerEl.GetString() ?? "";
                    if (!string.IsNullOrEmpty(lastPeer))
                    {
                        var combo = this.FindControl<ComboBox>("cmbPeers");
                        if (combo != null)
                        {
                            combo.ItemsSource = new List<string> { lastPeer };
                            combo.SelectedItem = lastPeer;
                        }
                    }
                }
                LoadPeerFolderStates(doc);
                // Carpeta local guardada
                if (doc.TryGetProperty("localPath", out var lpEl))
                {
                    var lp = lpEl.GetString() ?? "";
                    if (!string.IsNullOrEmpty(lp) && Directory.Exists(lp))
                    {
                        _localPath = lp;
                        _ = RefreshLocalAsync();
                    }
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
            if (doc.TryGetProperty("safeModeEnabled", out var safeModeEl))
            {
                _safeModeEnabled = safeModeEl.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkSafeMode");
                    if (chk != null) chk.IsChecked = _safeModeEnabled;
                });
            }
            if (doc.TryGetProperty("safeModeNoRemoteDelete", out var safeEl))
            {
                _safeModeNoRemoteDelete = safeEl.GetBoolean();
                _server.SafeModeNoRemoteDelete = _safeModeNoRemoteDelete;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkSafeModeNoDelete");
                    if (chk != null) chk.IsChecked = _safeModeNoRemoteDelete;
                });
            }
            if (doc.TryGetProperty("remotePowerEnabled", out var rpeEl))
            {
                _remotePowerEnabled = rpeEl.GetBoolean();
                _server.RemotePowerEnabled = _remotePowerEnabled;
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
            if (doc.TryGetProperty("requireHighRiskApproval", out var hraEl))
            {
                _requireHighRiskApproval = hraEl.GetBoolean();
                _server.ApproveHighRisk = _requireHighRiskApproval ? OnApproveHighRiskAsync : null;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkRequireHighRiskApproval");
                    if (chk != null) chk.IsChecked = _requireHighRiskApproval;
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
            if (doc.TryGetProperty("autoClipboard", out var autoClip))
            {
                _autoClipboard = autoClip.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkAutoClipboard");
                    if (chk != null) chk.IsChecked = _autoClipboard;
                });
            }
            if (doc.TryGetProperty("autoOpenLinks", out var autoOpen))
            {
                _autoOpenLinks = autoOpen.GetBoolean();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chk = this.FindControl<CheckBox>("chkAutoOpenLinks");
                    if (chk != null) chk.IsChecked = _autoOpenLinks;
                });
            }
            var startupSecurity = StartupSettings.Load(SettingsPath);
            _tlsEnabled = startupSecurity.TlsEnabled;
            _restrictShareRoot = startupSecurity.RestrictShareRoot;
            _readOnly = startupSecurity.ReadOnly;
            _safeModeEnabled = startupSecurity.SafeModeEnabled;
            _safeModeNoRemoteDelete = startupSecurity.SafeModeNoRemoteDelete;
            _remotePowerEnabled = startupSecurity.RemotePowerEnabled;
            _requireApproval = startupSecurity.RequireApproval;
            _requireHighRiskApproval = startupSecurity.RequireHighRiskApproval;
            _server.TlsEnabled = _tlsEnabled;
            _server.RestrictToShareRoot = _restrictShareRoot;
            _server.ReadOnly = _readOnly;
            _server.SafeModeNoRemoteDelete = _safeModeNoRemoteDelete;
            _server.RemotePowerEnabled = _remotePowerEnabled;
            _server.ApproveIncoming = _requireApproval ? OnApproveIncomingAsync : null;
            _server.ApproveHighRisk = _requireHighRiskApproval ? OnApproveHighRiskAsync : null;
            await Dispatcher.UIThread.InvokeAsync(() => ApplySafeModePolicy(persist: false, showStatus: false));
            if (doc.TryGetProperty("bandwidthLimitMbps", out var bwEl))
            {
                var bwValue = bwEl.ValueKind switch
                {
                    JsonValueKind.Number => bwEl.TryGetInt32(out var n) ? n : 0,
                    JsonValueKind.String when int.TryParse(bwEl.GetString(), out var n) => n,
                    _ => 0
                };
                _bandwidthLimitMbps = Math.Clamp(bwValue, 0, 12);
            }
            RateLimiter.Global.BytesPerSecond = _bandwidthLimitMbps <= 0 ? 0 : (long)_bandwidthLimitMbps * 1024 * 1024;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var slider = this.FindControl<Slider>("sldBandwidth");
                if (slider != null) slider.Value = _bandwidthLimitMbps;
                var lbl = this.FindControl<TextBlock>("txtBwValue");
                if (lbl != null) lbl.Text = _bandwidthLimitMbps <= 0 ? L["bw.unlimited"] : $"{_bandwidthLimitMbps} MB/s";
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
                catch (Exception ex) { Log.Error("persistence", "load-settings", new { error = ex.Message }); }
            });
            // Feature 3: perfiles
            if (doc.TryGetProperty("profiles", out var profilesEl))
            {
                _profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(profilesEl.GetRawText()) ?? new();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Restaurar perfil seleccionado por nombre guardado; si no, buscar por IP.
                    string? select = null;
                    if (doc.TryGetProperty("selectedProfile", out var spEl))
                        select = spEl.GetString();
                    if (string.IsNullOrEmpty(select))
                    {
                        var curIp = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
                        var curPort = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "";
                        select = _profiles.FirstOrDefault(p => p.Ip == curIp && p.Port == curPort)?.Name;
                    }
                    RefreshProfilesCombo(select);
                });
            }
        }
        catch (Exception ex) { Log.Error("persistence", "load-settings", new { error = ex.Message }); }
    }

    private void LoadPeerFolderStates(JsonElement doc)
    {
        try
        {
            if (!doc.TryGetProperty("peerFolders", out var peerFoldersEl) || peerFoldersEl.ValueKind != JsonValueKind.Object) return;
            _peerFolders.Clear();
            foreach (var peerEl in peerFoldersEl.EnumerateObject())
            {
                if (peerEl.Value.ValueKind != JsonValueKind.Object) continue;
                var localPath = peerEl.Value.TryGetProperty("localPath", out var localEl)
                    ? localEl.GetString() ?? ""
                    : peerEl.Value.TryGetProperty("LocalPath", out var legacyLocalEl) ? legacyLocalEl.GetString() ?? "" : "";
                var remotePath = peerEl.Value.TryGetProperty("remotePath", out var remoteEl)
                    ? remoteEl.GetString() ?? ""
                    : peerEl.Value.TryGetProperty("RemotePath", out var legacyRemoteEl) ? legacyRemoteEl.GetString() ?? "" : "";
                _peerFolders[peerEl.Name] = new PeerFolderState(localPath, remotePath);
            }
        }
        catch (Exception ex) { Log.Warn("persistence", "load-peer-folders-failed", new { error = ex.Message }); }
    }

    private void ApplyPeerFolderState(string ip, string port)
    {
        var key = PeerFolderKey(ip, port);
        if (string.IsNullOrEmpty(key)) return;

        if (_peerFolders.TryGetValue(key, out var folders))
        {
            _localPath = !string.IsNullOrWhiteSpace(folders.LocalPath) && Directory.Exists(folders.LocalPath)
                ? folders.LocalPath
                : "";
            _remotePath = folders.RemotePath ?? "";
        }
        else
        {
            _localPath = "";
            _remotePath = "";
        }

        UpdateLocalPath();
        UpdateRemotePath();
    }

    private void RememberCurrentPeerFolders(bool force = false)
    {
        if (!force && _client == null) return;
        var ip = this.FindControl<TextBox>("txtRemoteIp")?.Text?.Trim() ?? "";
        var port = this.FindControl<TextBox>("txtRemotePort")?.Text?.Trim() ?? "8742";
        var key = PeerFolderKey(ip, port);
        if (string.IsNullOrEmpty(key)) return;

        _peerFolders[key] = new PeerFolderState(_localPath ?? "", _remotePath ?? "");
        SaveSettingsDeferred(ip, port);
    }

    private static string PeerFolderKey(string ip, string port)
    {
        ip = ip.Trim();
        port = port.Trim();
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(port)) return "";
        return $"{ip}:{port}";
    }
    // P6: versi�n debounceda para callers frecuentes (combo, perfiles)
    private DispatcherTimer? _saveDebounce;
    private string _savePendingIp = "", _savePendingPort = "8742";

    private void SaveSettingsDeferred(string ip, string port)
    {
        _savePendingIp = ip; _savePendingPort = port;
        if (_saveDebounce == null)
        {
            _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveDebounce.Tick += (_, _) => { _saveDebounce?.Stop(); SaveSettings(_savePendingIp, _savePendingPort); };
        }
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void SaveSettings(string ip, string port)
    {
        // BUG-FIX-B1: SettingsLock.Wait() bloqueaba el hilo de UI si PersistLanguage (Loc.cs)
        // retenia el lock. Solucion: capturar todos los datos UI aqui (en el hilo de UI, no
        // necesita lock), serializar a JSON string (rapido, sin I/O), y escribir en background.
        // Paso 1: Capturar datos de UI (solo valido en el hilo de UI)
        var pin       = this.FindControl<TextBox>("txtPin")?.Text?.Trim() ?? "";
        var lastPeer  = this.FindControl<ComboBox>("cmbPeers")?.SelectedItem as string ?? "";
        var selProfile= this.FindControl<ComboBox>("cmbProfiles")?.SelectedItem as string ?? "";
        var localPort = this.FindControl<TextBox>("txtLocalPort")?.Text?.Trim() ?? "8742";
        var peerFolders = _peerFolders.ToDictionary(kv => kv.Key, kv => new { localPath = kv.Value.LocalPath, remotePath = kv.Value.RemotePath }, StringComparer.OrdinalIgnoreCase);
        var snapshot = new
        {
            remoteIp  = ip, remotePort = port, localPath = _localPath, peerFolders,
            lastPeer, selectedProfile = selProfile, localPort, pin,
            tlsEnabled = _tlsEnabled, restrictShareRoot = _restrictShareRoot,
            readOnly = _readOnly, safeModeEnabled = _safeModeEnabled, safeModeNoRemoteDelete = _safeModeNoRemoteDelete,
            remotePowerEnabled = _remotePowerEnabled,
            requireApproval = _requireApproval, requireHighRiskApproval = _requireHighRiskApproval, compressEnabled = _compressEnabled,
            autoClipboard = _autoClipboard, autoOpenLinks = _autoOpenLinks,
            bandwidthLimitMbps = _bandwidthLimitMbps, advancedMode = _advancedMode,
            welcomeShown = _welcomeShown, language = L.Current, theme = _theme,
            winW = this.Width, winH = this.Height,
            winX = this.Position.X, winY = this.Position.Y,
            winMax = this.WindowState == WindowState.Maximized,
            profiles = _profiles
        };
        // Paso 2: Serializar en el hilo de UI (rapido, sin I/O de disco)
        var json = JsonSerializer.Serialize(snapshot);
        // Paso 3: Escribir en background para no bloquear el hilo de UI
        _ = Task.Run(async () =>
        {
            await Localization.Loc.SettingsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                JsonStore.WriteRawAtomic(SettingsPath, json);
            }
            catch (Exception ex) { Log.Error("persistence", "save-settings", new { error = ex.Message }); }
            finally { Localization.Loc.SettingsLock.Release(); }
        });
    }

}


