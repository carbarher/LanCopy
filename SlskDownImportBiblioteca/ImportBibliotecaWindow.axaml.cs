using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SlskDownBibliotecaImport;
using SlskDownBibliotecaImport.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDownImportBiblioteca;

public partial class ImportBibliotecaWindow : Window
{
    private const string DiagnosticEnvVar = "SLSDOWN_IMPORT_DIAGNOSTIC";
    private CancellationTokenSource? _importCts;
    private readonly CalibreConverterService _calibre = new();
    private readonly object _logLock = new();
    private readonly StringBuilder _pendingLog = new();
    private int _logFlushScheduled;
    private bool _closeRequestedAfterCancel;
    private bool _allowImmediateClose;

    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public ImportBibliotecaWindow()
    {
        InitializeComponent();

        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlskDownImportBiblioteca", "logs");
        _logFilePath = Path.Combine(_logDirectory, "import_biblioteca.log");
        try
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
        }
        catch (Exception ex)
        {
            TryAppendDiagnosticToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No se pudo crear carpeta de logs: {ex.Message}{Environment.NewLine}");
        }

        Closing += OnClosing;
        Closed += OnClosed;
        Opened += (_, _) => _ = LoadPathsFromConfigAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowImmediateClose)
            return;

        if (_importCts == null)
            return;

        _importCts.Cancel();
        _closeRequestedAfterCancel = true;
        e.Cancel = true;

        if (TxtStatus != null)
            TxtStatus.Text = "Cancelando importacion... cerrando al terminar";
        AppendLog("⏹ Cierre solicitado: cancelando importación para evitar borrados durante salida.");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _calibre.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TryAppendDiagnosticToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DisposeAsync Calibre falló: {ex}{Environment.NewLine}");
            }
        });
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
                if (ChkDryRun != null) ChkDryRun.IsChecked = cfg.ImportDryRun;
                if (ChkPipelineTxt != null) ChkPipelineTxt.IsChecked = cfg.ImportPipelineMissingTxt;
                if (ChkSourceCleanup != null) ChkSourceCleanup.IsChecked = cfg.ImportSourceCleanupEnabled;
                if (ChkDeleteUnknownOnCleanup != null) ChkDeleteUnknownOnCleanup.IsChecked = cfg.ImportDeleteUnknownOnCleanup;
                if (ChkPurgeSource != null) ChkPurgeSource.IsChecked = cfg.PurgeSourceAfterImport;
            });
        }
        catch (Exception ex)
        {
            AppendDiagnostic($"Carga de configuración: {ex}");
        }
    }

    private static bool IsDiagnosticEnabled()
    {
        var value = Environment.GetEnvironmentVariable(DiagnosticEnvVar);
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value == "1"
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private void AppendDiagnostic(string message, bool mirrorToUi = true)
    {
        TryAppendDiagnosticToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        if (mirrorToUi && IsDiagnosticEnabled())
            AppendLog($"[diag] {message}");
    }

    private void TryAppendDiagnosticToFile(string text)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.AppendAllText(_logFilePath, text);
        }
        catch { }
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
            AppendLog("⚠️ En la configuración de IMP no hay formatos de importación habilitados.");
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
            HashDedupForce = cfg.ImportHashDedupForce, // Cargado correctamente de la configuración
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
            PipelineTxt = ChkPipelineTxt?.IsChecked == true,
        };

        // Omitimos ApplyImportPreset(o) para respetar la configuración cargada del usuario
        // ApplyImportPreset(o);
        o.DryRun = ChkDryRun?.IsChecked == true;
        o.PipelineTxt = ChkPipelineTxt?.IsChecked == true;
        o.GenTxtRequested = o.GenTxt;
        cfg.ImportSourceCleanupEnabled = ChkSourceCleanup?.IsChecked == true;
        cfg.ImportDeleteUnknownOnCleanup = ChkDeleteUnknownOnCleanup?.IsChecked == true;
        cfg.PurgeSourceAfterImport = ChkPurgeSource?.IsChecked == true;

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
        cfg.ImportPipelineMissingTxt = o.PipelineTxt;

        var saveResult = await ImportAppConfigStore.SaveAsync(cfg);

        ClearLog();
        AppendLog($"📂 Origen: {src}");
        AppendLog($"📂 Destino: {dest}");
        AppendLog("⚙️ Motor SlskDownBibliotecaImport (sin app principal)…");
        if (!saveResult.Success)
        {
            AppendLog($"⚠️ No se pudo guardar la configuración de importación: {saveResult.ErrorMessage}");
            AppendDiagnostic($"Guardado de configuración falló: {saveResult.ErrorMessage}", mirrorToUi: false);
        }

        BtnImport!.IsEnabled = false;
        if (Pb != null) { Pb.IsVisible = true; Pb.Value = 0; }
        if (BtnCancelRun != null) BtnCancelRun.IsVisible = true;
        TxtStatus!.Text = "Preparando…";

        _importCts = new CancellationTokenSource();
        var ct = _importCts.Token;

        if (!o.DryRun && (cfg.ImportSourceCleanupEnabled || cfg.PurgeSourceAfterImport))
        {
            var confirmed = await ConfirmDestructiveSourceOpsAsync(src, cfg.ImportSourceCleanupEnabled, cfg.PurgeSourceAfterImport);
            if (!confirmed)
            {
                AppendLog("⏹ Operación cancelada por el usuario antes de borrar en origen.");
                return;
            }
        }

        var importCheckpointPath = Path.Combine(dest, ".import_checkpoint.json");
        var importCheckpointDeltaPath = Path.Combine(dest, ".import_checkpoint.delta");
        var host = new StandaloneImportHost(this, _calibre, cfg.DownloadOnlyPublicDomain);
        var ui = new StandaloneImportUi(this);
        StandaloneGutenbergPublicDomainPolicy.ResetPolicyStats();

        try
        {
            // ── Verificación asíncrona de espacio en disco (Evita congelamiento de UI) ──
            double freeGb = 999.0;
            try
            {
                freeGb = await Task.Run(() =>
                {
                    var di = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dest))!);
                    return di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                }, ct);
            }
            catch { }

            if (freeGb < 2.0)
            {
                AppendLog($"❌ Espacio libre crítico ({freeGb:F1} GB). Importación cancelada.");
                return;
            }
            if (freeGb < 8.0)
            {
                o.MinimalLog = true;
                o.FastMode = true;
                if (!o.HashDedupForce) o.HashDedup = false;
                AppendLog($"⚠️ Poco espacio ({freeGb:F1} GB): modo rápido de seguridad{(o.HashDedupForce ? " (hash forzado se mantiene)" : "")}.");
            }
            // ────────────────────────────────────────────────────────────────────────────
            if (cfg.ImportSourceCleanupEnabled)
            {
                // ── Limpieza previa de la carpeta de origen ──────────────────
                TxtStatus!.Text = "Limpiando carpeta de origen…";
                AppendLog("🧹 [Limpieza] Borrando archivos no válidos en origen...");
                var catalogPathOverride = Environment.GetEnvironmentVariable("SLSDOWN_GUTENBERG_AUTHORS_PATH");
                var gutenbergTokens = await Task.Run(() => SourceFolderCleaner.LoadGutenbergTokens(catalogPathOverride, src), ct);
                var catalogSnapshot = await Task.Run(() => StandaloneGutenbergPublicDomainPolicy.GetCatalogSnapshot(src), ct);
                var cleanProgress = new Progress<string>(AppendLog); // marshala automáticamente al hilo UI
                if (catalogSnapshot.AuthorCount > 0)
                {
                    AppendLog($"📚 [Gutenberg] Catálogo activo: {catalogSnapshot.AuthorCount:N0} autor(es) desde {catalogSnapshot.SourcePath}");
                }
                else
                {
                    AppendLog("⚠️ [Gutenberg] No se cargó ningún autor desde catálogo; la política PD rechazará por seguridad.");
                }
                var cleanResult = await SourceFolderCleaner.CleanAsync(src, gutenbergTokens, cleanProgress, o.DryRun, cfg.ImportDeleteUnknownOnCleanup, ct);
                AppendLog($"📊 [Limpieza resumen] escaneados {cleanResult.ScannedTotal:N0} · libros {cleanResult.ScannedBooks:N0} · borrados {cleanResult.Deleted:N0} (no-libro {cleanResult.NonBookDeleted:N0}, desconocido {cleanResult.UnknownDeleted:N0}) · omitidos {cleanResult.Skipped:N0}");
                AppendLog("✅ [Limpieza] Borrado finalizado.");
                // ─────────────────────────────────────────────────────────────
            }
            else
            {
                AppendLog("🧹 [Limpieza] Desactivada (opción local de IMP).");
            }

            TxtStatus!.Text = "Importando…";
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

            if (cfg.PurgeSourceAfterImport)
            {
                AppendLog("🗑️ [Purge origen] Borrando remanentes del origen...");
                var purgeResult = await SourceFolderCleaner.PurgeRemainingBooksAndEmptyDirsAsync(src, new Progress<string>(AppendLog), o.DryRun, ct);
                AppendLog($"📊 [Purge resumen] remanentes detectados {purgeResult.RemainingFilesFound:N0} · borrados {purgeResult.RemainingFilesDeleted:N0} · carpetas vacías eliminadas {purgeResult.EmptyDirectoriesDeleted:N0} · errores {purgeResult.DeleteErrors:N0}");
                AppendLog("✅ [Purge origen] Borrado finalizado.");
            }
            else
            {
                AppendLog("🧾 [Purge origen] Omitido (Borrar remanentes en origen está desactivado).");
            }

            var policyStats = StandaloneGutenbergPublicDomainPolicy.GetPolicyStatsSnapshot();
            if (policyStats.Evaluated > 0)
            {
                AppendLog($"📚 [PD] Evaluados {policyStats.Evaluated:N0} · aceptados {policyStats.Accepted:N0} · rechazados {policyStats.Rejected:N0}");
                AppendLog($"   ↳ no literario {policyStats.RejectedNonLiterary:N0} · sin autor {policyStats.RejectedNoAuthor:N0} · sin catálogo {policyStats.RejectedNoCatalog:N0} · autor fuera de Gutenberg {policyStats.RejectedAuthorNotInCatalog:N0}");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("⏹ Cancelado.");
        }
        catch (Exception ex)
        {
            AppendLog($"❌ {ex.Message}");
            AppendDiagnostic($"Importación falló: {ex}", mirrorToUi: false);
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

            if (_closeRequestedAfterCancel)
            {
                _allowImmediateClose = true;
                await Dispatcher.UIThread.InvokeAsync(Close);
            }
        }
    }

    private async Task<bool> ConfirmDestructiveSourceOpsAsync(string srcDir, bool sourceCleanupEnabled, bool purgeEnabled)
    {
        var message = sourceCleanupEnabled && purgeEnabled
            ? $"Se van a ejecutar operaciones de borrado en origen.\n\nCarpeta: {srcDir}\n\n- Limpieza previa puede borrar archivos por reglas Gutenberg\n- Purge final puede borrar remanentes\n\n¿Continuar?"
            : sourceCleanupEnabled
                ? $"Se va a ejecutar limpieza de origen (puede borrar archivos por reglas Gutenberg).\n\nCarpeta: {srcDir}\n\n¿Continuar?"
                : $"Se va a borrar remanente en origen al finalizar.\n\nCarpeta: {srcDir}\n\n¿Continuar?";

        var tcs = new TaskCompletionSource<bool>();
        var dlg = new Window
        {
            Title = "Confirmar operaciones destructivas",
            Width = 620,
            Height = 260,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(14),
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Atencion: esta operacion puede borrar archivos en la carpeta de origen.",
                                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Children =
                                {
                                    new Button { Content = "Cancelar", MinWidth = 110 },
                                    new Button { Content = "Continuar", MinWidth = 110 }
                                }
                            }
                        }
                    }
                }
            }
        };

        var panel = (StackPanel)((DockPanel)dlg.Content!).Children[0]!;
        var actions = (StackPanel)panel.Children[2]!;
        var btnCancel = (Button)actions.Children[0]!;
        var btnContinue = (Button)actions.Children[1]!;

        btnCancel.Click += (_, _) => { tcs.TrySetResult(false); dlg.Close(); };
        btnContinue.Click += (_, _) => { tcs.TrySetResult(true); dlg.Close(); };
        dlg.Closed += (_, _) => tcs.TrySetResult(false);

        await dlg.ShowDialog(this);
        return await tcs.Task;
    }

    internal void AppendLog(string line)
    {
        lock (_logLock)
        {
            _pendingLog.Append(line).Append(Environment.NewLine);
        }

        if (Interlocked.CompareExchange(ref _logFlushScheduled, 1, 0) == 0)
            Dispatcher.UIThread.Post(FlushPendingLog, DispatcherPriority.Background);
    }

    internal void ClearLog()
    {
        lock (_logLock)
        {
            _pendingLog.Clear();
        }

        if (TxtLog != null)
            TxtLog.Text = string.Empty;
    }

    private void FlushPendingLog()
    {
        string chunk;
        lock (_logLock)
        {
            chunk = _pendingLog.ToString();
            _pendingLog.Clear();
        }

        if (chunk.Length > 0)
        {
            // Escribir en segundo plano asincrónico al archivo físico
            var fileChunk = chunk;
            Task.Run(() =>
            {
                try
                {
                    File.AppendAllText(_logFilePath, fileChunk);
                }
                catch (Exception ex)
                {
                    TryAppendDiagnosticToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Flush de log falló: {ex.Message}{Environment.NewLine}");
                }
            });

            if (TxtLog != null)
            {
                var newText = (TxtLog.Text ?? string.Empty) + chunk;
                if (newText.Length > 80000)
                {
                    // Truncar para no ahogar la renderización de Avalonia (guardando los últimos 50 KB)
                    int keepIndex = newText.Length - 50000;
                    int nextNewLine = newText.IndexOf('\n', keepIndex);
                    if (nextNewLine != -1 && nextNewLine < newText.Length - 100)
                    {
                        newText = "[... logs antiguos guardados en logs\\import_biblioteca.log ...]\n" + newText.Substring(nextNewLine + 1);
                    }
                    else
                    {
                        newText = "[... logs antiguos guardados en logs\\import_biblioteca.log ...]\n" + newText.Substring(keepIndex);
                    }
                }
                TxtLog.Text = newText;

                // Hacer auto-scroll al final del TextBox
                TxtLog.CaretIndex = TxtLog.Text.Length;
            }
        }

        Interlocked.Exchange(ref _logFlushScheduled, 0);

        lock (_logLock)
        {
            if (_pendingLog.Length > 0 && Interlocked.CompareExchange(ref _logFlushScheduled, 1, 0) == 0)
                Dispatcher.UIThread.Post(FlushPendingLog, DispatcherPriority.Background);
        }
    }

    private sealed class StandaloneImportHost : IBibliotecaImportHost
    {
        private readonly ImportBibliotecaWindow _w;
        private readonly CalibreConverterService _calibre;
        private readonly bool _pdFilterEnabled;

        public StandaloneImportHost(ImportBibliotecaWindow w, CalibreConverterService calibre, bool pdFilterEnabled)
        {
            _w = w;
            _calibre = calibre;
            _pdFilterEnabled = pdFilterEnabled;
        }

        public void LogClear() => Dispatcher.UIThread.Post(_w.ClearLog);
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

        public Task<ConversionResult> EnqueueCalibreTxtAsync(string inputPath, string outputDir, CancellationToken ct = default) =>
            _calibre.EnqueueConversionAsync(inputPath, outputDir, ct);

        public bool ShouldImportByPublicDomainPolicy(string destFileName, string? sourcePathOrEntry, out string? reason)
        {
            reason = null;

            if (!_pdFilterEnabled)
                return true;

            if (StandaloneGutenbergPublicDomainPolicy.ShouldReject(destFileName, sourcePathOrEntry, out var pdReason))
            {
                reason = pdReason;
                return false;
            }

            return true;
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
