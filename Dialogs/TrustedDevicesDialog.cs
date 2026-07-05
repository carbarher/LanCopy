using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;
using LanCopy.Services;
using System;
using System.Linq;

namespace LanCopy;

internal sealed class TrustedDevicesDialog : Window
{
    private static Loc L => Loc.Instance;
    private readonly ListBox _list = new();
    private readonly TextBlock _details = new();
    private readonly TextBlock _trustSummary = new();
    private readonly List<CertTrust.KnownHost> _devices = new();
    private readonly PeerPermissionStore _store = PeerPermissionStore.Shared;
    private readonly ComboBox _cmbTrustLevel = new() { Width = 170 };
    private readonly CheckBox _chkBrowse = MakeCheckBox(Loc.Instance["dlg.devices.browse"]);
    private readonly CheckBox _chkDownload = MakeCheckBox(Loc.Instance["dlg.devices.download"]);
    private readonly CheckBox _chkUpload = MakeCheckBox(Loc.Instance["dlg.devices.upload"]);
    private readonly CheckBox _chkModify = MakeCheckBox(Loc.Instance["dlg.devices.modify"]);
    private readonly CheckBox _chkDelete = MakeRiskCheckBox(Loc.Instance["dlg.devices.delete"]);
    private readonly CheckBox _chkSync = MakeRiskCheckBox(Loc.Instance["dlg.devices.sync"]);
    private readonly CheckBox _chkClipboard = MakeRiskCheckBox(Loc.Instance["dlg.devices.clipboard"]);
    private readonly CheckBox _chkPower = MakeRiskCheckBox(Loc.Instance["dlg.devices.power"]);

    public TrustedDevicesDialog()
    {
        Title = L["dlg.devices.title"];
        Width = 760;
        Height = 520;
        CanResize = true;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new DockPanel { Margin = new Thickness(16) };

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.header"],
            Foreground = SolidColorBrush.Parse("#FFD700"),
            FontWeight = FontWeight.Bold,
            FontSize = 16
        });
        header.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.subtitle"],
            Foreground = SolidColorBrush.Parse("#D0D0D0"),
            TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(actions, Dock.Bottom);
        panel.Children.Add(actions);

        var btnSavePermissions = MakeBtn(L["dlg.devices.savePermissions"], "#2E7D32");
        var btnResetPermissions = MakeBtn(L["dlg.devices.resetSafe"], "#795548");
        var btnMakeTrusted = MakeBtn(L["dlg.devices.makeTrusted"], "#1565C0");
        var btnRestrict = MakeBtn(L["dlg.devices.restrict"], "#616161");
        var btnForgetSelected = MakeBtn(L["dlg.devices.forgetSelected"], "#C0392B");
        var btnForgetAll = MakeBtn(L["dlg.devices.forgetAll"], "#8E24AA");
        var btnCopyFingerprint = MakeBtn(L["dlg.devices.copyFingerprint"], "#455A64");
        ToolTip.SetTip(btnCopyFingerprint, L["dlg.devices.copyFingerprintTip"]);
        var btnClose = MakeBtn(Loc.Instance["dlg.ok"], "#3E3E42");

        btnClose.Click += (_, _) => Close();
        btnSavePermissions.Click += (_, _) => SavePermissionsForSelected();
        btnResetPermissions.Click += (_, _) => ResetPermissionsForSelected();
        btnMakeTrusted.Click += (_, _) => MakeTrustedForSelected();
        btnRestrict.Click += (_, _) => RestrictSelectedDevice();
        btnForgetSelected.Click += (_, _) => ForgetSelected();
        btnForgetAll.Click += (_, _) => ForgetAllDevices();
        btnCopyFingerprint.Click += async (_, _) => await CopyFingerprintAsync();

        actions.Children.Add(btnForgetAll);
        actions.Children.Add(btnRestrict);
        actions.Children.Add(btnMakeTrusted);
        actions.Children.Add(btnResetPermissions);
        actions.Children.Add(btnSavePermissions);
        actions.Children.Add(btnForgetSelected);
        actions.Children.Add(btnCopyFingerprint);
        actions.Children.Add(btnClose);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,2*"),
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        panel.Children.Add(grid);

        _list.Background = SolidColorBrush.Parse("#1E1E1E");
        _list.Foreground = SolidColorBrush.Parse("#E6E6E6");
        _list.BorderBrush = SolidColorBrush.Parse("#3F3F46");
        _list.SelectionChanged += (_, _) => UpdateDetails();
        Grid.SetColumn(_list, 0);
        Grid.SetRow(_list, 1);
        grid.Children.Add(_list);

        var rightPane = new StackPanel { Margin = new Thickness(12, 0, 0, 0), Spacing = 10 };
        rightPane.Children.Add(_details);
        rightPane.Children.Add(_trustSummary);
        rightPane.Children.Add(BuildTrustEditor());
        rightPane.Children.Add(BuildPermissionsEditor());
        Grid.SetColumn(rightPane, 1);
        Grid.SetRow(rightPane, 1);
        grid.Children.Add(rightPane);

        Content = panel;
        Reload();
    }

    private StackPanel BuildTrustEditor()
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.trustLevel"],
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            FontWeight = FontWeight.Bold
        });
        _cmbTrustLevel.ItemsSource = Enum.GetNames<CertTrust.PeerTrustLevel>();
        stack.Children.Add(_cmbTrustLevel);
        stack.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.trustLevelDesc"],
            Foreground = SolidColorBrush.Parse("#AFAFAF"),
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.trustedNoAutoRisk"],
            Foreground = SolidColorBrush.Parse("#AFAFAF"),
            TextWrapping = TextWrapping.Wrap
        });
        return stack;
    }

    private StackPanel BuildPermissionsEditor()
    {
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.permissions"],
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            FontWeight = FontWeight.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = L["dlg.devices.safeDefaults"],
            Foreground = SolidColorBrush.Parse("#AFAFAF"),
            TextWrapping = TextWrapping.Wrap
        });

        var presetRow = new WrapPanel();
        presetRow.Children.Add(MakePresetButton(L["dlg.devices.presetReadOnly"], () => ApplyPreset(Preset.ReadOnly)));
        presetRow.Children.Add(MakePresetButton(L["dlg.devices.presetSendReceive"], () => ApplyPreset(Preset.SendReceive)));
        presetRow.Children.Add(MakePresetButton(L["dlg.devices.presetFullShare"], () => ApplyPreset(Preset.FullShare)));
        presetRow.Children.Add(MakePresetButton(L["dlg.devices.presetAdvancedTrusted"], () => ApplyPreset(Preset.AdvancedTrusted)));
        stack.Children.Add(presetRow);

        stack.Children.Add(BuildPermissionGroup(L["dlg.devices.safeGroup"], new[]
        {
            _chkBrowse, _chkDownload, _chkUpload, _chkModify
        }));
        stack.Children.Add(BuildPermissionGroup(L["dlg.devices.riskyGroup"], new[]
        {
            _chkDelete, _chkSync, _chkClipboard, _chkPower
        }, true));

        return stack;
    }

    private static StackPanel BuildPermissionGroup(string title, IEnumerable<CheckBox> checks, bool risky = false)
    {
        var group = new StackPanel { Spacing = 4 };
        group.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = risky ? SolidColorBrush.Parse("#FF8A80") : SolidColorBrush.Parse("#E6E6E6"),
            FontWeight = FontWeight.Bold
        });
        foreach (var chk in checks)
        {
            chk.Margin = new Thickness(0, 0, 0, 2);
            group.Children.Add(chk);
        }
        return group;
    }

    private void Reload()
    {
        _devices.Clear();
        _devices.AddRange(CertTrust.ListKnownHosts());
        _list.ItemsSource = _devices.Select(d => $"{d.DeviceName}  ({d.LastAddress})").ToList();
        if (_devices.Count > 0) _list.SelectedIndex = 0;
        else _details.Text = L["dlg.devices.noDevices"];
    }

    private void UpdateDetails()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count)
        {
            _details.Text = "";
            return;
        }

        var d = _devices[idx];
        var lastSeen = d.LastSeenUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown";
        var permUpdated = _store.GetLastUpdatedUtc(d.Host)?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown";
        _trustSummary.Text = $"{L["dlg.devices.trustColor"]}: {DescribeTrustLevel(d.TrustLevel)}\n{L["dlg.devices.permUpdated"]}: {permUpdated}";
        _trustSummary.Foreground = TrustBrush(d.TrustLevel);
        _details.Text =
            $"{L["dlg.devices.name"]}: {d.DeviceName}\n" +
            $"{L["dlg.devices.host"]}: {d.Host}\n" +
            $"{L["dlg.devices.trust"]}: {d.TrustLevel}\n" +
            $"{L["dlg.devices.lastAddress"]}: {d.LastAddress}\n" +
            $"{L["dlg.devices.lastSeen"]}: {lastSeen}\n\n" +
            $"{L["dlg.devices.fingerprint"]}:\n{d.Fingerprint}\n\n" +
            $"{L["dlg.devices.fingerprintShort"]}: {d.FingerprintShort}\n\n" +
            DescribeTrustLevel(d.TrustLevel);
        _cmbTrustLevel.SelectedItem = d.TrustLevel.ToString();
        LoadPermissions(d.Host);
    }

    private string DescribeTrustLevel(CertTrust.PeerTrustLevel trustLevel)
        => trustLevel switch
        {
            CertTrust.PeerTrustLevel.Unknown => L["dlg.devices.unknown"],
            CertTrust.PeerTrustLevel.Paired => L["dlg.devices.paired"],
            CertTrust.PeerTrustLevel.Trusted => L["dlg.devices.trusted"],
            CertTrust.PeerTrustLevel.OwnerDevice => L["dlg.devices.ownerDevice"],
            _ => L["dlg.devices.unknownTrust"]
        };

    private static IBrush TrustBrush(CertTrust.PeerTrustLevel trustLevel)
        => trustLevel switch
        {
            CertTrust.PeerTrustLevel.Unknown => SolidColorBrush.Parse("#AFAFAF"),
            CertTrust.PeerTrustLevel.Paired => SolidColorBrush.Parse("#64B5F6"),
            CertTrust.PeerTrustLevel.Trusted => SolidColorBrush.Parse("#81C784"),
            CertTrust.PeerTrustLevel.OwnerDevice => SolidColorBrush.Parse("#FFD54F"),
            _ => SolidColorBrush.Parse("#AFAFAF")
        };

    private void LoadPermissions(string host)
    {
        var p = _store.Get(host);
        _chkBrowse.IsChecked = p.Browse;
        _chkDownload.IsChecked = p.Download;
        _chkUpload.IsChecked = p.Upload;
        _chkModify.IsChecked = p.Modify;
        _chkDelete.IsChecked = p.Delete;
        _chkSync.IsChecked = p.Sync;
        _chkClipboard.IsChecked = p.Clipboard;
        _chkPower.IsChecked = p.Power;
    }

    private void SavePermissionsForSelected()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var host = _devices[idx].Host;
        var trustLevel = ParseSelectedTrustLevel();
        CertTrust.SetTrustLevel(host, trustLevel);
        _store.Set(host, new PeerPermissionStore.Permissions(
            Browse: _chkBrowse.IsChecked == true,
            Download: _chkDownload.IsChecked == true,
            Upload: _chkUpload.IsChecked == true,
            Modify: _chkModify.IsChecked == true,
            Delete: _chkDelete.IsChecked == true,
            Sync: _chkSync.IsChecked == true,
            Clipboard: _chkClipboard.IsChecked == true,
            Power: _chkPower.IsChecked == true));
        Reload();
    }

    private void ResetPermissionsForSelected()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var host = _devices[idx].Host;
        CertTrust.SetTrustLevel(host, CertTrust.PeerTrustLevel.Paired);
        _store.Set(host, new PeerPermissionStore.Permissions());
        _cmbTrustLevel.SelectedItem = CertTrust.PeerTrustLevel.Paired.ToString();
        LoadPermissions(host);
        Reload();
    }

    private enum Preset
    {
        ReadOnly,
        SendReceive,
        FullShare,
        AdvancedTrusted
    }

    private void ApplyPreset(Preset preset)
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;

        var host = _devices[idx].Host;
        var permissions = preset switch
        {
            Preset.ReadOnly => new PeerPermissionStore.Permissions(Browse: true, Download: true),
            Preset.SendReceive => new PeerPermissionStore.Permissions(Browse: true, Download: true, Upload: true),
            Preset.FullShare => new PeerPermissionStore.Permissions(Browse: true, Download: true, Upload: true, Modify: true),
            Preset.AdvancedTrusted => new PeerPermissionStore.Permissions(Browse: true, Download: true, Upload: true, Modify: true, Delete: true, Sync: true, Clipboard: true, Power: true),
            _ => new PeerPermissionStore.Permissions()
        };

        var trust = preset == Preset.AdvancedTrusted
            ? CertTrust.PeerTrustLevel.Trusted
            : CertTrust.PeerTrustLevel.Paired;

        CertTrust.SetTrustLevel(host, trust);
        _store.Set(host, permissions);
        Reload();
    }

    private void MakeTrustedForSelected()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var host = _devices[idx].Host;
        CertTrust.SetTrustLevel(host, CertTrust.PeerTrustLevel.Trusted);
        Reload();
    }

    private void RestrictSelectedDevice()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var host = _devices[idx].Host;
        CertTrust.SetTrustLevel(host, CertTrust.PeerTrustLevel.Paired);
        _store.Set(host, new PeerPermissionStore.Permissions());
        Reload();
    }

    private void ForgetSelected()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var selected = _devices[idx];
        CertTrust.ForgetHost(selected.Host);
        _store.Remove(selected.Host);
        Reload();
    }

    private void ForgetAllDevices()
    {
        foreach (var device in _devices.ToList())
        {
            CertTrust.ForgetHost(device.Host);
            _store.Remove(device.Host);
        }
        Reload();
    }

    private CertTrust.PeerTrustLevel ParseSelectedTrustLevel()
    {
        var selected = _cmbTrustLevel.SelectedItem?.ToString();
        return Enum.TryParse<CertTrust.PeerTrustLevel>(selected, out var parsed)
            ? parsed
            : CertTrust.PeerTrustLevel.Paired;
    }

    private static CheckBox MakeCheckBox(string label)
    {
        var chk = new CheckBox
        {
            Content = label,
            Foreground = SolidColorBrush.Parse("#E6E6E6")
        };
        ToolTip.SetTip(chk, label);
        return chk;
    }

    private static CheckBox MakeRiskCheckBox(string label)
    {
        var chk = new CheckBox
        {
            Content = label,
            Foreground = SolidColorBrush.Parse("#FF8A80")
        };
        ToolTip.SetTip(chk, $"{L["dlg.devices.riskyPermission"]}: {label}");
        return chk;
    }

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(10, 5)
    };

    private static Button MakePresetButton(string label, Action onClick)
    {
        var btn = MakeBtn(label, "#263238");
        btn.Margin = new Thickness(0, 0, 6, 0);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private async Task CopyFingerprintAsync()
    {
        var idx = _list.SelectedIndex;
        if (idx < 0 || idx >= _devices.Count) return;
        var fp = _devices[idx].Fingerprint;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(fp);
    }
}
