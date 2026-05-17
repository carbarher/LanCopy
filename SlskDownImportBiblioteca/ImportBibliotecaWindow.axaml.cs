using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SlskDownAvalonia.Models;
using SlskDownBibliotecaImport;
using SlskDownBibliotecaImport.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDownImportBiblioteca;

public partial class ImportBibliotecaWindow : Window
{
    private CancellationTokenSource? _importCts;
    private readonly CalibreConverterService _calibre = new();

    public ImportBibliotecaWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _calibre.Dispose();
        Opened += (_, _) => _ = LoadPathsFromConfigAsync();
    }

    private async Task LoadPathsFromConfigAsync()
    {
        try
        {
            var cfg = await ImportAppConfigStore.LoadAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (TxtSource != null) TxtSource.Text = cfg.ImportSourceDir ?? "";
                if (TxtDest != null) TxtDest.Text = cfg.DownloadDirectory ?? "";
            });
        }
        catch { /* rutas vacías */ }
    }

    /// <summary>Siempre usa el modo completo (sin presets, sin modo noche).</summary>
    private static void ApplyImportPreset(BibliotecaImportRuntimeState o)
    {
        // Modo completo fijo (equivalente al antiguo preset 0)
        o.SkipDup = true;
        o.WantArchive = true;
        o.QuickSig = true;
        o.WarmCache = true;
        o.AutoPause = true;
        o.Prioritize = true;
        o.MinimalLog = false;
        o.QualityScan = true;
        o.HashDedup = true;
        o.HashDedupForce = false;
        o.NightAuto = false;
        o.FastMode = false;
        o.GenTxt = true;
        o.MinBytes = 200L * 1024;
    }

    private async void BtnBrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Carpeta origen" }).ConfigureAwait(true);
        if (folders.Count > 0 && TxtSource != null)
            TxtSource.Text = folders[0].Path.LocalPath;
    }

    private async void BtnBrowseDest_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Carpeta de biblioteca (descargas)" }).ConfigureAwait(true);
        if (folders.Count > 0 && TxtDest != null)
        {
            var p = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(p)) TxtDest.Text = p;
        }
    }

    private void BtnCancelRun_Click(object? sender, RoutedEventArgs e) => _importCts?.Cancel();

    private async void BtnImport_Click(object? sender, RoutedEventArgs e)
    {
        if (_importCts != null) return;

        var src = TxtSource?.Text?.Trim() ?? "";
        var dest = TxtDest?.Text?.Trim() ?? "";
        switch (MaintenanceImportPathGuards.Validate(dest, src))
        {
            case MaintenanceImportPathGuards.PathValidationCode.DestMissingOrInvalid:
                AppendLog("⚠️ Carpeta de destino no válida o no configurada.");
                return;
            case MaintenanceImportPathGuards.PathValidationCode.SrcMissingOrInvalid:
                AppendLog("⚠️ Carpeta origen no válida.");
                return;
            case MaintenanceImportPathGuards.PathValidationCode.SameDirectory:
                AppendLog("⚠️ Origen y destino no pueden ser la misma carpeta.");
                return;
            case MaintenanceImportPathGuards.PathValidationCode.NestedDirectoryConflict:
                AppendLog("⚠️ Origen y destino no pueden estar anidados entre sí.");
                return;
            case MaintenanceImportPathGuards.PathValidationCode.PathError:
                AppendLog("⚠️ Rutas no válidas.");
                return;
        }

        // Sin ConfigureAwait(false): el resto del handler lee CheckBox/ComboBox (hilo UI obligatorio).
        var cfg = await ImportAppConfigStore.LoadAsync();

        var allowed = BibliotecaImportOptions.BuildAllowedImportExtensions(
            cfg.ImportEpub, cfg.ImportMobi, cfg.ImportPdf, cfg.ImportFb2, cfg.ImportAzw3, cfg.ImportDjvu, cfg.ImportTxt);
        if (allowed.Count == 0)
        {
            AppendLog("⚠️ En config no hay formatos de importación — marca al menos uno en la app principal (Configuración / import).");
            return;
        }

        var o = new BibliotecaImportRuntimeState
        {
            WantArchive = cfg.ImportArchive,
            GenTxt = cfg.ImportGenTxt,
            GenTxtRequested = cfg.ImportGenTxt,
            SkipDup = cfg.ImportSkipDup,
            DryRun = ChkDryRun?.IsChecked == true,
            HashDedup = cfg.ImportHashDedup,
            FastMode = cfg.ImportFastMode,
            QuickSig = cfg.ImportQuickSignatureDedup,
            MinimalLog = cfg.ImportMinimalLog,
            WarmCache = cfg.ImportWarmCache,
            AutoPause = cfg.ImportAutoPause,
            Prioritize = cfg.ImportPrioritizeBestFormats,
            OnlyNew = cfg.ImportOnlyNewByDate,
            OnlyNewDays = cfg.ImportOnlyNewDays,
            QualityScan = cfg.ImportQualityScan,
            NightAuto = cfg.ImportNightModeAuto,
            NightStart = cfg.ImportNightStartHour,
            NightEnd = cfg.ImportNightEndHour,
            MinBytes = cfg.ImportMinKB * 1024L,
            PipelineVerify = ChkPipelineVerify?.IsChecked == true,
            PipelineTxt = ChkPipelineTxt?.IsChecked == true,
        };

        // Siempre usa modo completo (sin presets)
        ApplyImportPreset(o);
        o.DryRun = ChkDryRun?.IsChecked == true;
        o.PipelineVerify = ChkPipelineVerify?.IsChecked == true;
        o.PipelineTxt = ChkPipelineTxt?.IsChecked == true;
        o.GenTxtRequested = o.GenTxt;

        cfg.ImportSourceDir = src;
        cfg.DownloadDirectory = dest;
        cfg.ImportArchive = o.WantArchive;
        cfg.ImportGenTxt = o.GenTxt;
        cfg.ImportSkipDup = o.SkipDup;
        cfg.ImportHashDedup = o.HashDedup;
        cfg.ImportHashDedupForce = o.HashDedupForce;
        cfg.ImportDryRun = o.DryRun;
        cfg.ImportMinKB = (int)Math.Clamp(o.MinBytes / 1024, 1, 1_000_000);
        cfg.ImportFastMode = o.FastMode;
        cfg.ImportQuickSignatureDedup = o.QuickSig;
        cfg.ImportMinimalLog = o.MinimalLog;
        cfg.ImportWarmCache = o.WarmCache;
        cfg.ImportAutoPause = o.AutoPause;
        cfg.ImportPrioritizeBestFormats = o.Prioritize;
        cfg.ImportOnlyNewByDate = o.OnlyNew;
        cfg.ImportOnlyNewDays = o.OnlyNewDays;
        cfg.ImportQualityScan = o.QualityScan;
        cfg.ImportNightModeAuto = o.NightAuto;
        cfg.ImportNightStartHour = o.NightStart;
        cfg.ImportNightEndHour = o.NightEnd;
        cfg.ImportPipelineFullVerify = o.PipelineVerify;
        cfg.ImportPipelineMissingTxt = o.PipelineTxt;

        try
        {
            var di = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dest))!);
            var freeGb = di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            if (freeGb < 2.0)
            {
                AppendLog($"❌ Espacio libre crítico ({freeGb:F1} GB).");
                return;
            }
            if (freeGb < 8.0)
            {
                o.MinimalLog = true;
                o.FastMode = true;
                if (!o.HashDedupForce) o.HashDedup = false;
                AppendLog($"⚠️ Poco espacio ({freeGb:F1} GB): modo rápido de seguridad{(o.HashDedupForce ? " (hash forzado se mantiene)" : "")}.");
            }
        }
        catch { }

        await ImportAppConfigStore.SaveImportFieldsAsync(cfg);

        if (TxtLog != null) TxtLog.Text = "";
        AppendLog($"📂 Origen: {src}");
        AppendLog($"📂 Destino: {dest}");
        AppendLog("⚙️ Motor SlskDownBibliotecaImport (sin app principal)…");

        BtnImport!.IsEnabled = false;
        if (Pb != null) { Pb.IsVisible = true; Pb.Value = 0; }
        if (BtnCancelRun != null) BtnCancelRun.IsVisible = true;
        TxtStatus!.Text = "Preparando…";

        _importCts = new CancellationTokenSource();
        var ct = _importCts.Token;

        var importCheckpointPath = Path.Combine(dest, ".import_checkpoint.json");
        var importCheckpointDeltaPath = Path.Combine(dest, ".import_checkpoint.delta");
        var host = new StandaloneImportHost(this, _calibre);
        var ui = new StandaloneImportUi(this);

        try
        {
            await BibliotecaImportEngine.RunAsync(
                src,
                dest,
                allowed,
                importCheckpointPath,
                importCheckpointDeltaPath,
                o,
                host,
                ui,
                retryOnlyDestFileNames: null,
                ct);
        }
        catch (OperationCanceledException)
        {
            AppendLog("⏹ Cancelado.");
        }
        catch (Exception ex)
        {
            AppendLog($"❌ {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                BtnImport.IsEnabled = true;
                if (Pb != null) Pb.IsVisible = false;
                if (BtnCancelRun != null) BtnCancelRun.IsVisible = false;
                _importCts?.Dispose();
                _importCts = null;
            });
        }
    }

    internal void AppendLog(string line)
    {
        if (TxtLog == null) return;
        TxtLog.Text += line + Environment.NewLine;
    }

    private sealed class StandaloneImportHost : IBibliotecaImportHost
    {
        private readonly ImportBibliotecaWindow _w;
        private readonly CalibreConverterService _calibre;
        private readonly bool _pdFilterEnabled;

        public StandaloneImportHost(ImportBibliotecaWindow w, CalibreConverterService calibre)
        {
            _w = w;
            _calibre = calibre;
            // Load PD setting synchronously (default to true if config unavailable)
            try
            {
                var cfg = LoadConfigSync();
                _pdFilterEnabled = cfg.DownloadOnlyPublicDomain;
            }
            catch
            {
                _pdFilterEnabled = true; // Default to enabled
            }
        }

        private static SlskDownAvalonia.Models.AppConfig LoadConfigSync()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlskDownAvalonia", "config.json");
            if (!File.Exists(path))
                return new SlskDownAvalonia.Models.AppConfig();
            try
            {
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<SlskDownAvalonia.Models.AppConfig>(json)
                    ?? new SlskDownAvalonia.Models.AppConfig();
            }
            catch
            {
                return new SlskDownAvalonia.Models.AppConfig();
            }
        }

        public void LogClear() => Dispatcher.UIThread.Post(() => { if (_w.TxtLog != null) _w.TxtLog.Text = ""; });
        public void Log(string line) => Dispatcher.UIThread.Post(() => _w.AppendLog(line));
        public void Status(string line) => Dispatcher.UIThread.Post(() => { if (_w.TxtStatus != null) _w.TxtStatus.Text = line; });
        public void PipelineQueue(string text, bool persistSnapshot = false) =>
            Dispatcher.UIThread.Post(() => _w.AppendLog("[pipeline] " + text));

        public int ActiveDownloads => 0;

        public void PostToUi(Action action) => Dispatcher.UIThread.Post(action);

        public void InvokeOnUiThread(Action action)
        {
            if (action == null) return;
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Invoke(action);
        }

        public bool CalibreAvailable => _calibre.IsAvailable;

        public bool SupportsPipelineFullVerify => false;

        public Task<ConversionResult> EnqueueCalibreTxtAsync(string inputPath, string outputDir, CancellationToken ct = default) =>
            _calibre.EnqueueConversionAsync(inputPath, outputDir, ct);

        // Simplified PD validation for standalone import tool (no heavy dependencies)
        private static readonly HashSet<string> s_nonLiteraryTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "magazine", "journal", "newspaper", "periodical", "music", "mp3", "flac",
            "bootleg", "audiobook", "podcast", "video", "film", "movie", "manual",
            "guide", "catalog", "map", "atlas", "concert", "festival"
        };

        public bool ShouldImportByPublicDomainPolicy(string destFileName, string? sourcePathOrEntry, out string? reason)
        {
            reason = null;

            // PD filtering check (value loaded in constructor from AppConfig)
            if (!_pdFilterEnabled)
                return true;

            // Quick non-literary check
            var fn = Path.GetFileNameWithoutExtension(destFileName);
            foreach (var token in s_nonLiteraryTokens)
            {
                if (fn.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"Token no literario: {token}";
                    return false;
                }
            }

            // Try to extract author from path/filename
            var author = TryExtractAuthor(destFileName, sourcePathOrEntry);
            if (string.IsNullOrWhiteSpace(author))
            {
                reason = "Autor no identificable";
                return false;
            }

            // In standalone mode: accept if author looks like "Lastname, Firstname" pattern
            // (simplified validation - full Gutenberg catalog only available in main app)
            if (author.Contains(',') || author.Contains('_') || author.Contains(" - "))
            {
                return true;
            }

            reason = "Formato de autor no reconocido (se espera 'Apellido, Nombre')";
            return false;
        }

        private static string? TryExtractAuthor(string fileName, string? fullPath)
        {
            // Try from parent folder name
            if (!string.IsNullOrEmpty(fullPath))
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var parent = Path.GetFileName(dir);
                    if (!string.IsNullOrWhiteSpace(parent) && parent.Length > 2)
                        return parent;
                }
            }

            // Try from filename: "Author - Title.ext" or "Author_Title.ext"
            var fn = Path.GetFileNameWithoutExtension(fileName);
            var separators = new[] { " - ", "_", " – " };
            foreach (var sep in separators)
            {
                var idx = fn.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                    return fn[..idx].Trim();
            }

            return null;
        }

        /// <summary>App dedicada: sin detector ONNX; aplica solo tamaño y filtros de calidad.</summary>
        public bool ShouldEnqueueTxtForImport(string destFileName, string destFullPath, long sizeBytes, bool qualityScan)
        {
            const long minBytesForTxt = 200L * 1024L;
            if (sizeBytes < minBytesForTxt) return false;
            if (qualityScan)
            {
                if (destFileName.Contains("unknown", StringComparison.OrdinalIgnoreCase)) return false;
                if (destFileName.Count(ch => ch == '_' || ch == '-') > 16) return false;
            }
            return true;
        }

        public void NotifyImportCompleted(int copied) { }

        public Task RebuildLibraryIndexIfNeededAsync(string importDest, CancellationToken ct) => Task.CompletedTask;

        public Task RunPipelineFullVerifyAsync(string importDest, CancellationToken ct)
        {
            // No debería llamarse si SupportsPipelineFullVerify es false; el motor omite esta ruta.
            Log("⚠️ Verificación completa post-import: usa la app principal (Mantenimiento → pestaña correspondiente).");
            return Task.CompletedTask;
        }

        public async Task RunPipelineMissingTxtAsync(string importDest, CancellationToken ct)
        {
            if (!_calibre.IsAvailable)
            {
                Log("ℹ️ Calibre no disponible — TXTs del pipeline omitidos.");
                return;
            }
            var prog = new Progress<(int current, int total, string fileName)>(tuple =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_w.Pb != null)
                    {
                        _w.Pb.IsVisible = true;
                        _w.Pb.Maximum = Math.Max(1, tuple.total);
                        _w.Pb.Value = Math.Clamp(tuple.current, 0, tuple.total);
                    }
                    if (_w.TxtStatus != null)
                    {
                        var fn = tuple.fileName;
                        if (fn.Length > 52) fn = fn[..49] + "…";
                        _w.TxtStatus.Text = tuple.total > 0
                            ? $"🔊 Post-import TXT [{tuple.current}/{tuple.total}] {fn}"
                            : $"🔊 Post-import TXT {fn}";
                    }
                });
            });
            var batch = await _calibre.ConvertAllToTxtAsync(importDest, importDest, prog, ct).ConfigureAwait(false);
            Log($"✅ Conversión a TXT terminada: {batch.Converted} convertidos · {batch.Skipped} ya existían · {batch.Failed} errores");
        }

        public void SetLastImportFailed(IReadOnlyList<ImportCandidate> failed) { }

        public void SetLastImportManifestPath(string? path) { }
    }

    private sealed class StandaloneImportUi : IBibliotecaImportUi
    {
        private readonly ImportBibliotecaWindow _w;

        public StandaloneImportUi(ImportBibliotecaWindow w) => _w = w;

        public void SetProgressMax(int max)
        {
            if (_w.Pb != null)
            {
                _w.Pb.Maximum = max;
                _w.Pb.Value = 0;
            }
        }

        public void SetProgressValue(int value)
        {
            if (_w.Pb != null) _w.Pb.Value = value;
        }

        public void SetStatus(string text)
        {
            if (_w.TxtStatus != null) _w.TxtStatus.Text = text;
        }

        public void SetPerf(string text) { }
    }
}
