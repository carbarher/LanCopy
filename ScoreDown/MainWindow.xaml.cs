using ScoreDown.Infrastructure;
using ScoreDown.Models;
using ScoreDown.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WinInput = System.Windows.Input;
using WinForms = System.Windows.Forms;
using WinDrag = System.Windows.DragEventArgs;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;

namespace ScoreDown;

public partial class MainWindow : Window, IAsyncDisposable
{
    private sealed class SourceStats
    {
        public DateTime StartedUtc = DateTime.UtcNow;
        public int CurrentParallelism;
        public int TotalItems;
        public int ItemsSeen;
        public int DownloadedItems;
        public int ExistingItems;
        public int DownloadedFiles;
        public long DownloadedBytes;
        public int SkippedFiles;
        public int RejectedItems;
        public int ErroredItems;
        public int FailedFiles;
        public int CancelledItems;
        public int CancelledFiles;
    }

    private readonly MutopiaService _mutopia = new();
    private readonly CpdlService _cpdl = new();
    private readonly MusopenService _musopen = new();
    private readonly DownloadService _downloader = new();

    private readonly ObservableCollection<PartituraItem> _allResults = new();
    private readonly ObservableCollection<DownloadQueueItem> _downloadQueue = [];
    private readonly ObservableCollection<DownloadHistoryItem> _downloadHistory = [];
    private readonly ObservableCollection<AudiverisLogItem> _audiverisLog = [];
    private string _currentDestFolder = string.Empty;
    // R8: CollectionViewSource wraps _allResults; ApplyFilter() only refreshes the view.
    private readonly CollectionViewSource _resultsView = new();
    private List<PartituraItem> _filtered = new();
    private List<PartituraItem> _offlineLibraryItems = [];
    // R7: thread-safe cache (concurrent searches from Task.WhenAll)
    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, DateTimeOffset LastAccessed, List<PartituraItem> Results)> _searchCache = new();
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(10);
    private const int MaxCacheEntries = 20;
    private readonly List<string> _recentQueries = new();
    private readonly Dictionary<string, string> _savedTags = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxRecentQueries = 12;
    // R3: debounce timer for tag filter
    private readonly DispatcherTimer _tagFilterDebounce;
    private static readonly string[] KnownComposers =
    [
        "Bach", "J. S. Bach", "Mozart", "Beethoven", "Chopin", "Liszt", "Schubert", "Schumann",
        "Brahms", "Handel", "Haydn", "Debussy", "Ravel", "Vivaldi", "Palestrina", "Victoria",
        "Monteverdi", "Purcell", "Fauré", "Mendelssohn", "Tchaikovsky", "Dvorak", "Grieg"
    ];
    private readonly string _historyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "search-history.json");
    private readonly string _uiStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "ui-state.json");
    private readonly string _tagsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "tags.json");
    private readonly string _downloadHistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "download-history.json");
    private readonly string _offlineLibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "offline-library.json");
    private readonly string _audiverisPageFailuresPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "audiveris-page-failures.json");
    private readonly string _audiverisTimeoutFamiliesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "audiveris-timeout-families.json");
    private readonly string _audiverisTimeoutStrikesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "audiveris-timeout-strikes.json");
    private bool _enableMutopia = ReadFeatureFlag("SCOREDOWN_ENABLE_MUTOPIA", true);
    private bool _enableCpdl = ReadFeatureFlag("SCOREDOWN_ENABLE_CPDL", false);
    private bool _enableMusopen = ReadFeatureFlag("SCOREDOWN_ENABLE_MUSOPEN", true);
    private readonly ConcurrentDictionary<string, int> _liveErrorTypes = new(StringComparer.OrdinalIgnoreCase);
    private int _cacheHits;
    private int _cacheMisses;
    private int _sessionSearches;
    private int _sessionResults;
    private int _sessionDownloads;
    private long _sessionBytes;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _catalogCts;
    // Sesión WebView2 para CPDL — rutar peticiones por WebView2 preserva TLS fingerprint de Cloudflare
    private Infrastructure.CpdlWebSession? _cpdlWebSession;
    // Dedup set para catálogo — O(1) lookup vs O(N²) .Any()
    private readonly HashSet<string> _catalogSeenKeys = new(StringComparer.OrdinalIgnoreCase);
    // Cache de compositores conocidos en resultados actuales — evita iterar _allResults por tecla
    private readonly HashSet<string> _knownComposersCache = new(StringComparer.OrdinalIgnoreCase);
    // Cache de lista merged para sugerencias de compositor — invalida cuando cambia _knownComposersCache
    private List<string>? _composerSuggestionsPool;
    private int _composerPoolVersion;  // inc al añadir a _knownComposersCache
    private static readonly string[] AudiverisInputExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"];
    private static readonly string[] AudiverisOutputExtensions = [".mxl", ".xml", ".mscz", ".mscx"];
    private readonly int _audiverisBatchSize = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_BATCH", 100, 1, 10000);
    private readonly int _audiverisTimeoutSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_SEC", 300, 30, 7200);
    private readonly int _audiverisParallel = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_PARALLEL", 2, 1, 8);
    private readonly int _audiverisPdfTimeoutMinSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_MIN_SEC", 420, 60, 7200);
    private readonly int _audiverisPdfTimeoutPerMbSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_PER_MB_SEC", 12, 0, 300);
    private readonly int _audiverisPdfTimeoutMaxSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_MAX_SEC", 1800, 120, 10800);
    private readonly int _audiverisTimeoutStrikeBoostSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_STRIKE_BOOST_SEC", 60, 0, 600);
    private readonly int _audiverisTimeoutCooldownMinutes = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_COOLDOWN_MIN", 720, 10, 10080);
    private readonly int _audiverisStrikeDecayHours = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_STRIKE_DECAY_HOURS", 48, 1, 8760);
    private readonly HashSet<string> _audiverisKnownPageFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _audiverisTimeoutFamilies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _audiverisTimeoutStrikes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _audiverisStrikeLastUtc = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _audiverisRunning;

    private PartituraItem? _contextItem;  // item bajo clic derecho
    private readonly ConcurrentDictionary<string, byte> _pausedQueueFiles = new(StringComparer.OrdinalIgnoreCase);

    // Production features: file logging + circuit breaker
    private FileLoggingService? _fileLogger;
    private GlobalCircuitBreaker? _circuitBreaker;
    private int _cleanupDone;
    private int _shutdownRequested;
    // ── Tray / toast ─────────────────────────────────────────────────────
    // R6: NotifyIcon initialized in ctor (after InitializeComponent) to avoid pre-UI GDI access.
    private WinForms.NotifyIcon _notifyIcon = null!;

    private void ShowToast(string title, string body, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        _notifyIcon.Visible = true;
        _notifyIcon.ShowBalloonTip(4000, title, body, icon);
    }

    public MainWindow()
    {
        InitializeComponent();

        // R6: safe to access GDI resources after InitializeComponent
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "ScoreDown",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = false
        };

        // R3: debounce timer (250 ms single-shot)
        _tagFilterDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _tagFilterDebounce.Tick += OnTagFilterDebounceTick;

        LoadUiState();
        // R8: wire CollectionViewSource to _allResults
        _resultsView.Source = _allResults;
        lstResults.ItemsSource = _resultsView.View;

        // Production features initialization
        _fileLogger = new FileLoggingService();
        _circuitBreaker = new GlobalCircuitBreaker();
        _fileLogger.Log("=== ScoreDown Started ===");

        _cpdl.RequestInteractiveSessionAsync = OpenCpdlSessionAsync;

        chkEnableMutopia.IsChecked = _enableMutopia;
        chkEnableCpdl.IsChecked = _enableCpdl;
        chkEnableMusopen.IsChecked = _enableMusopen;

        _currentDestFolder = txtDestFolder.Text?.Trim() ?? string.Empty;
        LoadSearchHistory();
        LoadTags();
        LoadDownloadHistory();
        LoadOfflineLibrary();
        LoadAudiverisPageFailures();
        LoadAudiverisTimeoutFamilies();
        LoadAudiverisTimeoutStrikes();
        UpdateAudiverisStatus();
        RefreshHistoryCombo();
        UpdateCacheStats();
        lstQueue.ItemsSource = _downloadQueue;
        lstDownloadHistory.ItemsSource = _downloadHistory;
        lstAudiverisLog.ItemsSource = _audiverisLog;
        UpdateSessionStats();
        UpdateSourceDashboard();
    }

    private void Window_PreviewKeyDown(object sender, WinInput.KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != WinInput.ModifierKeys.Control) return;
        switch (e.Key)
        {
            case WinInput.Key.F:
                txtSearch.Focus();
                txtSearch.SelectAll();
                e.Handled = true;
                break;
            case WinInput.Key.K:
                txtLog.Clear();
                e.Handled = true;
                break;
            case WinInput.Key.D:
                if (btnDownload.IsEnabled)
                    BtnDownload_Click(btnDownload, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void RequestAppShutdown()
    {
        if (System.Threading.Interlocked.Exchange(ref _shutdownRequested, 1) == 1) return;
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Unificar flujo: X/Alt+F4 debe pasar por mismo cierre global que el botón Salir.
        if (System.Threading.Volatile.Read(ref _shutdownRequested) == 0)
        {
            e.Cancel = true;
            RequestAppShutdown();
            return;
        }

        CleanupRuntimeResources();
        SaveTags();
        SaveDownloadHistory();
        SaveOfflineLibrary();
        SaveAudiverisPageFailures();
        SaveAudiverisTimeoutFamilies();
        SaveAudiverisTimeoutStrikes();

        // Production cleanup: close marker + retention cleanup
        _fileLogger?.Log("=== ScoreDown Closed ===");
        _fileLogger?.Cleanup(keepDays: 30);

        if (_sessionSearches == 0 && _sessionDownloads == 0) return;

        var msg =
            $"Sesión:\n\n" +
            $"• Búsquedas: {_sessionSearches}\n" +
            $"• Obras vistas: {_sessionResults}\n" +
            $"• Archivos descargados: {_sessionDownloads}\n" +
            $"• Bytes descargados: {FormatBytes(_sessionBytes)}";
        DarkDialogService.ShowMessage(this, msg, "Resumen de sesión", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnTagFilterDebounceTick(object? sender, EventArgs e)
    {
        _tagFilterDebounce.Stop();
        ApplyFilter();
    }

    private void CleanupRuntimeResources()
    {
        if (System.Threading.Interlocked.Exchange(ref _cleanupDone, 1) == 1) return;

        try
        {
            _tagFilterDebounce.Stop();
            _tagFilterDebounce.Tick -= OnTagFilterDebounceTick;
        }
        catch { }

        try { _catalogCts?.Cancel(); } catch { }
        try { _searchCts?.Cancel(); } catch { }
        try { _downloadCts?.Cancel(); } catch { }

        try { _catalogCts?.Dispose(); } catch { }
        try { _searchCts?.Dispose(); } catch { }
        try { _downloadCts?.Dispose(); } catch { }

        _catalogCts = null;
        _searchCts = null;
        _downloadCts = null;

        try { _cpdlWebSession?.Dispose(); } catch { }
        _cpdlWebSession = null;
        _cpdl.WebViewFetchAsync = null;

        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch { }
    }

    public ValueTask DisposeAsync()
    {
        CleanupRuntimeResources();
        return ValueTask.CompletedTask;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized || txtSearch is null) return;
        UpdateComposerSuggestions(txtSearch.Text);
    }

    private void LstComposerSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstComposerSuggestions is null || popComposerSuggestions is null || txtSearch is null) return;
        if (lstComposerSuggestions.SelectedItem is not string suggestion) return;
        txtSearch.Text = suggestion;
        txtSearch.CaretIndex = txtSearch.Text.Length;
        popComposerSuggestions.IsOpen = false;
        txtSearch.Focus();
        lstComposerSuggestions.SelectedItem = null;
    }

    private void LstComposerSuggestions_MouseDoubleClick(object sender, WinInput.MouseButtonEventArgs e)
    {
        if (lstComposerSuggestions is null || popComposerSuggestions is null || txtSearch is null) return;
        if (lstComposerSuggestions.SelectedItem is string suggestion)
        {
            txtSearch.Text = suggestion;
            txtSearch.CaretIndex = txtSearch.Text.Length;
            popComposerSuggestions.IsOpen = false;
            txtSearch.Focus();
        }
    }

    private void UpdateComposerSuggestions(string? text)
    {
        if (lstComposerSuggestions is null || popComposerSuggestions is null || txtSearch is null) return;

        var query = text?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            popComposerSuggestions.IsOpen = false;
            return;
        }

        // Rebuild merged pool only when _knownComposersCache changes (version stamp)
        if (_composerSuggestionsPool is null || _composerPoolVersion != _knownComposersCache.Count)
        {
            _composerSuggestionsPool = KnownComposers
                .Concat(_knownComposersCache)
                .Concat(_savedTags.Keys.Select(ParseComposerFromSavedKey))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _composerPoolVersion = _knownComposersCache.Count;
        }

        var suggestions = _composerSuggestionsPool
            .Where(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.IndexOf(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Length)
            .Take(8)
            .ToList();

        lstComposerSuggestions.ItemsSource = suggestions;
        popComposerSuggestions.IsOpen = suggestions.Count > 0 && txtSearch.IsKeyboardFocusWithin;
    }

    private static string ParseComposerFromSavedKey(string key)
    {
        var firstSep = key.IndexOf('|');
        if (firstSep <= 0) return string.Empty;
        return key[..firstSep].Replace("_", " ").Trim();
    }

    // ── Panel info lateral ──────────────────────────────────────────────

    private void LstResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var item = lstResults.SelectedItem as PartituraItem;
        UpdateSelectedItemPanel(item);
    }

    private void UpdateSelectedItemPanel(PartituraItem? item)
    {
        if (item is null)
        {
            txtInfoHint.Visibility = Visibility.Visible;
            pnlInfoContent.Visibility = Visibility.Collapsed;
            txtTagEditor.Text = string.Empty;
            txtTagStatus.Text = string.Empty;
            UpdatePreview(null);
            return;
        }
        txtInfoHint.Visibility = Visibility.Collapsed;
        pnlInfoContent.Visibility = Visibility.Visible;
        txtInfoTitle.Text = item.Title;
        txtInfoComposer.Text = string.IsNullOrWhiteSpace(item.Composer) ? "(compositor desconocido)" : item.Composer;
        txtInfoSource.Text = item.Source;
        txtInfoUrl.Text = string.IsNullOrWhiteSpace(item.PageUrl) ? "(sin URL)" : item.PageUrl;
        txtInfoUrl.Tag = item.PageUrl;
        icInfoFiles.ItemsSource = item.Files;
        txtTagEditor.Text = item.UserTag;
        txtTagStatus.Text = string.IsNullOrWhiteSpace(item.UserTag) ? "Sin tag" : "Tag cargado";
        UpdatePreview(item);
    }

    private void BtnOpenComposerFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lstResults.SelectedItem is not PartituraItem item) return;
        OpenComposerFolder(item);
    }

    private void OpenComposerFolder(PartituraItem item)
    {
        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder))
        {
            Log("⚠️ Establece la carpeta de destino primero");
            return;
        }

        var folderPath = BuildDestSubFolder(destFolder, item);
        if (!Directory.Exists(folderPath))
        {
            Log($"⚠️ Carpeta no existe: {folderPath}");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
            Log($"📂 Abriendo: {folderPath}");
        }
        catch (Exception ex)
        {
            Log($"❌ Error abriendo carpeta: {ex.Message}");
        }
    }

    private void TxtTagEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        txtTagStatus.Text = lstResults.SelectedItem is PartituraItem ? "Cambios sin guardar" : string.Empty;
    }

    private void BtnSaveTag_Click(object sender, RoutedEventArgs e)
    {
        if (lstResults.SelectedItem is not PartituraItem item) return;
        item.UserTag = txtTagEditor.Text?.Trim() ?? string.Empty;
        var key = BuildTagKey(item);
        var legacyKey = BuildDedupKey(item);
        if (string.IsNullOrWhiteSpace(item.UserTag))
        {
            _savedTags.Remove(key);
            _savedTags.Remove(legacyKey);
        }
        else
        {
            _savedTags[key] = item.UserTag;
            _savedTags.Remove(legacyKey);
        }

        SaveTags();
        _composerSuggestionsPool = null;  // invalidar cache de sugerencias (savedTags cambió)
        txtTagStatus.Text = string.IsNullOrWhiteSpace(item.UserTag) ? "Tag limpiado" : "Tag guardado";
        lstResults.Items.Refresh();
    }

    private void BtnClearTag_Click(object sender, RoutedEventArgs e)
    {
        txtTagEditor.Text = string.Empty;
        BtnSaveTag_Click(sender, e);
    }

    private void TxtInfoUrl_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var url = (sender as System.Windows.Controls.TextBlock)?.Tag as string;
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    // ── Drag & drop carpeta ─────────────────────────────────────────

    private void TxtDestFolder_DragOver(object sender, WinDrag e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            var paths = (string[]?)e.Data.GetData(WpfDataFormats.FileDrop);
            e.Effects = (paths?.Length > 0 && Directory.Exists(paths[0]))
                ? WpfDragDropEffects.Copy
                : WpfDragDropEffects.None;
        }
        else
        {
            e.Effects = WpfDragDropEffects.None;
        }
        e.Handled = true;
    }

    private void TxtDestFolder_Drop(object sender, WinDrag e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            var paths = (string[]?)e.Data.GetData(WpfDataFormats.FileDrop);
            var folder = paths?.FirstOrDefault(Directory.Exists);
            if (!string.IsNullOrEmpty(folder))
            {
                txtDestFolder.Text = folder;
                _currentDestFolder = folder;
                SaveUiState();
                Log($"📁 Carpeta establecida: {folder}");
            }
        }
    }

    // ── Menú contextual ───────────────────────────────────────────────

    private void LstResults_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Detecta el item bajo el ratón para el context menu
        var hit = lstResults.InputHitTest(e.GetPosition(lstResults)) as FrameworkElement;
        _contextItem = null;
        while (hit is not null)
        {
            if (hit.DataContext is PartituraItem pi) { _contextItem = pi; break; }
            hit = (hit.Parent as FrameworkElement) ?? (VisualTreeHelper.GetParent(hit) as FrameworkElement);
        }
    }

    private IEnumerable<PartituraItem> ContextItems()
    {
        // Si hay multi-selección, aplica a todos los seleccionados; si no, al item bajo clic
        var sel = lstResults.SelectedItems.Cast<PartituraItem>().ToList();
        if (sel.Count > 1) return sel;
        return _contextItem is not null ? [_contextItem] : sel;
    }

    private void CtxMenuOpenPage_Click(object sender, RoutedEventArgs e)
    {
        var item = _contextItem ?? lstResults.SelectedItem as PartituraItem;
        if (item is null || string.IsNullOrWhiteSpace(item.PageUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.PageUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CtxMenuSelect_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in ContextItems()) i.IsSelected = true;
        lstResults.Items.Refresh();
        UpdateDownloadButton();
    }

    private void CtxMenuDeselect_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in ContextItems()) i.IsSelected = false;
        lstResults.Items.Refresh();
        UpdateDownloadButton();
    }

    private void CtxMenuOnlyPdf_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in ContextItems())
            i.IsSelected = i.Files.Any(f => f.Format == "PDF");
        lstResults.Items.Refresh();
        UpdateDownloadButton();
    }

    private void CtxMenuOnlyMidi_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in ContextItems())
            i.IsSelected = i.Files.Any(f => f.Format == "MIDI");
        lstResults.Items.Refresh();
        UpdateDownloadButton();
    }

    private void CtxMenuAllFormats_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in ContextItems()) i.IsSelected = true;
        lstResults.Items.Refresh();
        UpdateDownloadButton();
    }

    private async void CtxQueueRetry_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not DownloadQueueItem queueItem) return;
        if (queueItem.SourceFile is null || queueItem.SourceItem is null) return;

        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder)) return;

        var subFolder = BuildDestSubFolder(destFolder, queueItem.SourceItem);

        try
        {
            txtStatus.Text = $"Reintentando {queueItem.FileName}...";
            var progress = new Progress<(PartituraFile file, string state, double percent, double speedKBps)>(r =>
            {
                txtFileProgress.Visibility = Visibility.Visible;
                pbFileProgress.Visibility = Visibility.Visible;
                txtFileProgress.Text = $"↻ {r.file.FileName} · {r.speedKBps:F0} KB/s";
                pbFileProgress.Value = r.percent;
                UpdateQueue(r.file, r.state, r.percent, r.speedKBps);
            });

            var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress, file => _pausedQueueFiles.ContainsKey(file.DownloadUrl), _downloadCts?.Token ?? default);
            if (result.Success)
            {
                _sessionDownloads += result.Skipped ? 0 : 1;
                _sessionBytes += result.BytesDownloaded;
                UpdateSessionStats();
                RecordDownload(queueItem.SourceItem, queueItem.SourceFile, result, queueItem.SourceItem.Source);
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Error reintentando {queueItem.FileName}: {ex.Message}");
        }
        finally
        {
            pbFileProgress.Value = 0;
            pbFileProgress.Visibility = Visibility.Collapsed;
            txtFileProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void CtxQueuePause_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not DownloadQueueItem queueItem || queueItem.SourceFile is null) return;
        _pausedQueueFiles.TryAdd(queueItem.SourceFile.DownloadUrl, 0);
        queueItem.Status = "Pausado";
        lstQueue.Items.Refresh();
    }

    private async void CtxQueueResume_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not DownloadQueueItem queueItem || queueItem.SourceFile is null || queueItem.SourceItem is null) return;
        _pausedQueueFiles.TryRemove(queueItem.SourceFile.DownloadUrl, out _);
        queueItem.Status = "Reanudando";
        lstQueue.Items.Refresh();
        await RetryQueueItemAsync(queueItem);
    }

    private async Task RetryQueueItemAsync(DownloadQueueItem queueItem)
    {
        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder) || queueItem.SourceFile is null || queueItem.SourceItem is null) return;

        var subFolder = BuildDestSubFolder(destFolder, queueItem.SourceItem);

        var progress = new Progress<(PartituraFile file, string state, double percent, double speedKBps)>(r =>
        {
            txtFileProgress.Visibility = Visibility.Visible;
            pbFileProgress.Visibility = Visibility.Visible;
            txtFileProgress.Text = $"↻ {r.file.FileName} · {r.speedKBps:F0} KB/s";
            pbFileProgress.Value = r.percent;
            UpdateQueue(r.file, r.state, r.percent, r.speedKBps);
        });

        var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress, null, _downloadCts?.Token ?? default);
        if (result.Success)
            RecordDownload(queueItem.SourceItem, queueItem.SourceFile, result, queueItem.SourceItem.Source);
    }

    private void UpdatePreview(PartituraItem? item)
    {
        wbPreview.Visibility = Visibility.Collapsed;
        txtPreviewFallback.Visibility = Visibility.Visible;

        if (item is null)
        {
            txtPreviewHint.Text = string.Empty;
            txtPreviewFallback.Text = "Sin vista previa";
            return;
        }

        var pdf = item.Files.FirstOrDefault(f => f.Format == "PDF");
        if (pdf is not null && !string.IsNullOrWhiteSpace(pdf.DownloadUrl))
        {
            txtPreviewHint.Text = $"PDF: {pdf.FileName}";
            try
            {
                wbPreview.Navigate(pdf.DownloadUrl);
                wbPreview.Visibility = Visibility.Visible;
                txtPreviewFallback.Visibility = Visibility.Collapsed;
                return;
            }
            catch
            {
                txtPreviewFallback.Text = "No se pudo incrustar PDF. Usa abrir página/enlace.";
                return;
            }
        }

        var midi = item.Files.FirstOrDefault(f => f.Format == "MIDI");
        if (midi is not null)
        {
            txtPreviewHint.Text = $"MIDI: {midi.FileName}";
            txtPreviewFallback.Text = "Preview MIDI embebido no disponible. Usa descarga o abre página.";
            return;
        }

        txtPreviewHint.Text = "";
        txtPreviewFallback.Text = "Sin vista previa compatible";
    }

    // ── Búsqueda ──────────────────────────────────────────────────────────

    private void TxtSearch_KeyDown(object sender, WinInput.KeyEventArgs e)
    {
        if (e.Key == WinInput.Key.Enter) _ = DoSearchAsync();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
        => _ = DoSearchAsync();

    private void BtnCancelSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchCts?.Cancel();
    }

    // ── Catálogo completo ─────────────────────────────────────────────────

    private void BtnFetchCatalog_Click(object sender, RoutedEventArgs e) => _ = DoFetchCatalogAsync();

    private void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtDestFolder.Text))
        {
            var autoDest = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ScoreDown",
                "Partituras");
            Directory.CreateDirectory(autoDest);
            txtDestFolder.Text = autoDest;
            Log($"📁 Carpeta automática: {autoDest}");
        }

        var allItem = cmbSource.Items
            .OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(i => string.Equals(i.Content?.ToString(), "Todas", StringComparison.OrdinalIgnoreCase));
        if (allItem is not null)
            cmbSource.SelectedItem = allItem;

        _ = DoFetchCatalogAsync(forceAllSources: true);
    }

    private void BtnCancelCatalog_Click(object sender, RoutedEventArgs e) => _catalogCts?.Cancel();

    private async void BtnDeleteAndRedownload_Click(object sender, RoutedEventArgs e)
    {
        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder) || !Directory.Exists(destFolder))
        {
            DarkDialogService.ShowMessage(this, "Selecciona una carpeta de destino válida.", "Re-descargar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = DarkDialogService.ShowMessage(
            this,
            $"Se borrarán TODOS los archivos de:\n{destFolder}\n\nLuego se re-descargará todo desde cero.\n\n¿Continuar?",
            "Borrar y re-descargar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        // Borrar archivos (no subdirectorios)
        try
        {
            int deleted = 0;
            foreach (var file in Directory.EnumerateFiles(destFolder, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); deleted++; } catch { }
            }
            _audiverisLog.Clear();
            _downloadHistory.Clear();
            _downloadQueue.Clear();
            Log($"🗑 Borrados {deleted} archivo(s) de {destFolder}");
        }
        catch (Exception ex)
        {
            Log($"❌ Error borrando archivos: {ex.Message}");
            return;
        }

        // Re-descargar todo
        await Task.Delay(300); // pequeña pausa para que el log sea visible
        BtnDownloadAll_Click(sender, e);
    }

    /// <summary>
    /// Fase 1: cataloga metadatos de la fuente seleccionada (sin añadir a _allResults para evitar
    ///         O(N²) y freezes de UI con 150k+ items).
    /// Fase 2: descarga automáticamente todos los archivos, saltando los ya existentes en disco.
    ///         Mutopia / CPDL: descarga completa automática.
    /// </summary>
    private async Task DoFetchCatalogAsync(bool forceAllSources = false)
    {
        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder))
        {
            Log("⚠️ Establece la carpeta de destino antes de iniciar la descarga del catálogo");
            return;
        }

        // Check circuit breaker for rate limit protection
        if (_circuitBreaker is not null && !_circuitBreaker.AllowRequest())
        {
            var retry = _circuitBreaker.TimeUntilRetry();
            Log($"⏸️ Circuito abierto: demasiados errores. Reintenta en {retry.TotalSeconds:F0}s ({_circuitBreaker.GetStatus()})");
            return;
        }

        var source = (cmbSource.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Mutopia";
        if (forceAllSources)
            source = "Todas";
        if (source == "Offline")
        {
            Log("⚠️ Selecciona Mutopia, CPDL, Musopen o Todas para descargar catálogo");
            return;
        }

        if (!_enableCpdl && source == "CPDL")
        {
            Log("⏸️ CPDL está desactivado temporalmente (Cloudflare). Usa Mutopia o Todas.");
            return;
        }
        if (!_enableMutopia && source == "Mutopia")
        {
            Log("⏸️ Mutopia está desactivado por configuración. Usa CPDL o Todas.");
            return;
        }
        _catalogCts?.Cancel();
        _catalogCts?.Dispose();
        _catalogCts = new CancellationTokenSource();
        var ct = _catalogCts.Token;

        btnFetchCatalog.IsEnabled = false;
        btnDownloadAll.IsEnabled = false;
        btnCancelCatalog.IsEnabled = true;
        btnCancelAllDownloads.IsEnabled = true;
        ClearResults();
        _catalogSeenKeys.Clear();

        IProgress<string> progress = new Progress<string>(msg =>
        {
            if (Dispatcher.CheckAccess()) { txtStatus.Text = msg; Log(msg); }
            else Dispatcher.InvokeAsync(() => { txtStatus.Text = msg; Log(msg); });
        });

        // Acumula items thread-safe; no toca UI ni _allResults hasta fase 2
        var catalogItems = new ConcurrentBag<PartituraItem>();
        int totalFound = 0;
        // Claves de items YA en la biblioteca offline (para no duplicar en re-ejecución)
        // Usa el mismo BuildDedupKey + Source para coincidir con dedup de búsquedas normales
        var existingKeys = new HashSet<string>(
            _offlineLibraryItems.Select(i => $"{i.Source}|{BuildDedupKey(i)}"),
            StringComparer.OrdinalIgnoreCase);

        void OnItem(PartituraItem item)
        {
            var key = $"{item.Source}|{item.Title}|{item.Composer}";
            lock (_catalogSeenKeys)
            {
                if (!_catalogSeenKeys.Add(key)) return;  // O(1), ignora duplicados en esta sesión
            }
            catalogItems.Add(item);
            var n = System.Threading.Interlocked.Increment(ref totalFound);
            if (n % 500 == 0)
                Dispatcher.InvokeAsync(() => txtResultCount.Text = $"{n} obras catalogadas...");
        }

        try
        {
            // ── Fase 1: obtener metadatos ────────────────────────────────
            Log($"📚 Fase 1: catalogando {source}...");

            // Envolver cada fuente de forma independiente: si una falla no aborta las demás.
            static Task GuardedFetch(string sourceName, Task task, IProgress<string> p) =>
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted && !t.IsCanceled)
                        p.Report($"⚠️ Error catalogando {sourceName}: {t.Exception!.InnerException?.Message ?? t.Exception.Message}");
                }, TaskScheduler.Default);

            var fetchTasks = new List<Task>();
            if (_enableMutopia && (source is "Mutopia" or "Todas"))
                fetchTasks.Add(GuardedFetch("Mutopia", _mutopia.FetchAllAsync(OnItem, progress, ct), progress));
            if (_enableCpdl && (source is "CPDL" or "Todas"))
                fetchTasks.Add(GuardedFetch("CPDL", _cpdl.FetchAllAsync(OnItem, progress, ct), progress));
            if (_enableMusopen && _musopen.HasApiKey && (source is "Musopen" or "Todas"))
                fetchTasks.Add(GuardedFetch("Musopen", _musopen.FetchAllAsync(OnItem, progress, ct), progress));
            if (source == "Todas")
            {
                var disabled = new List<string>();
                if (!_enableCpdl) disabled.Add("CPDL");
                if (!_enableMutopia) disabled.Add("Mutopia");
                if (!_enableMusopen) disabled.Add("Musopen");
                if (disabled.Count > 0)
                    Log($"ℹ️ Fuentes desactivadas por configuración: {string.Join(", ", disabled)}");
            }

            await Task.WhenAll(fetchTasks).ConfigureAwait(false);

            // Vuelca a biblioteca offline en un solo paso (no cada 200 items)
            // Solo añade items NUEVOS — evita duplicados si el catálogo se ejecuta más de una vez
            await Dispatcher.InvokeAsync(() =>
            {
                int newCount = 0;
                foreach (var item in catalogItems)
                {
                    var key = $"{item.Source}|{BuildDedupKey(item)}";
                    if (existingKeys.Add(key))   // Add retorna false si ya estaba → no duplica
                    {
                        _offlineLibraryItems.Add(item);
                        newCount++;
                    }
                }
                txtResultCount.Text = $"{catalogItems.Count} obras catalogadas ({newCount} nuevas)";
            });
            SaveOfflineLibrary();

            Log($"✅ Fase 1: {catalogItems.Count} obras catalogadas y guardadas");

            // ── Fase 2: descargar archivos ───────────────────────────────
            var downloadable = catalogItems.ToList();

            downloadable = downloadable
                .Where(i =>
                    (_enableMutopia || !string.Equals(i.Source, "Mutopia", StringComparison.OrdinalIgnoreCase))
                    && (_enableCpdl || !string.Equals(i.Source, "CPDL", StringComparison.OrdinalIgnoreCase))
                    && (_enableMusopen || !string.Equals(i.Source, "Musopen", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var bySource = downloadable
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Source) ? "Desconocida" : i.Source)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{g.Key}:{g.Count()}")
                .ToArray();
            Log($"📦 Obras para descarga por fuente: {(bySource.Length == 0 ? "ninguna" : string.Join(" | ", bySource))}");

            downloadable = await ApplyBulkPreflightAsync(downloadable, progress, ct).ConfigureAwait(false);
            if (downloadable.Count == 0)
            {
                Log("⚠️ Preflight descartó todas las fuentes para descarga automática (baja disponibilidad de archivos)");
                return;
            }

            int totalProcessedItems = 0;
            int totalDownloadedItems = 0;
            int totalExistingItems = 0;
            int totalDownloadedFiles = 0;
            int totalSkippedFiles = 0;
            int totalRejectedItems = 0;
            int totalErroredItems = 0;
            int totalFailedFiles = 0;
            int totalCancelledItems = 0;
            int totalCancelledFiles = 0;
            int totalAutoStoppedItems = 0;
            int consecutiveErrors = 0;
            var sourceStats = new ConcurrentDictionary<string, SourceStats>(StringComparer.OrdinalIgnoreCase);
            var errorTypes = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cpdlWindow = new Queue<bool>();
            var cpdlWindowLock = new object();
            int cpdlNoFilesInWindow = 0;         // contador O(1) de false en la ventana deslizante
            int cpdlAutoStopActive = 0;
            int cpdlTotalProcessed = 0;          // total CPDL procesadas (sin contar skip-auto-stop)
            const int cpdlWindowSize = 1000;      // #2: ventana más grande
            const int cpdlWindowMin = 400;        // #2: mínimo muestras para evaluar
            const int cpdlGracePeriod = 500;      // #3: no evaluar hasta procesar N obras CPDL
            const double cpdlNoFilesStopRatio = 0.80;  // #9: ratio (bajado para compensar ventana mayor)
            const int cpdlNoFilesStopAbsMin = 200;     // #9: mínimo sin-archivos absolutos en ventana

            static SourceStats GetStats(ConcurrentDictionary<string, SourceStats> map, string source)
                => map.GetOrAdd(string.IsNullOrWhiteSpace(source) ? "Desconocida" : source, _ => new SourceStats());

            _liveErrorTypes.Clear();
            void AddErrorType(ConcurrentDictionary<string, int> map, string? error)
            {
                var key = string.IsNullOrWhiteSpace(error) ? "Desconocido" : error.Trim();
                map.AddOrUpdate(key, 1, (_, n) => n + 1);
                _liveErrorTypes.AddOrUpdate(key, 1, (_, n) => n + 1);
            }

            string BuildLiveSourceMetrics()
            {
                if (sourceStats.IsEmpty) return string.Empty;

                var now = DateTime.UtcNow;
                var parts = new List<string>();
                foreach (var kv in sourceStats.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var s = kv.Value;
                    var elapsedSec = Math.Max((now - s.StartedUtc).TotalSeconds, 1);
                    var elapsedMin = Math.Max(elapsedSec / 60.0, 1.0 / 60.0);
                    var worksPerMin = s.ItemsSeen / elapsedMin;
                    var mbPerSec = (s.DownloadedBytes / (1024.0 * 1024.0)) / elapsedSec;
                    var noFilesRatio = s.ItemsSeen > 0 ? (double)s.RejectedItems / s.ItemsSeen : 0;
                    parts.Add($"{kv.Key}: {worksPerMin:F1} ob/min · {mbPerSec:F2} MB/s · sin={noFilesRatio:P0} · p={Math.Max(1, s.CurrentParallelism)}");
                }

                return string.Join(" | ", parts);
            }

            void ReportProgressEvery50(bool silent = false)
            {
                var processed = System.Threading.Interlocked.Increment(ref totalProcessedItems);
                if (silent)
                    return;

                if (processed % 10 == 0)
                {
                    progress.Report($"📥 {processed}/{downloadable.Count} obras · nuevas={totalDownloadedItems} previas={totalExistingItems} sin-archivos={totalRejectedItems} error={totalErroredItems} · archivos ✅{totalDownloadedFiles} ⏭️{totalSkippedFiles} ❌{totalFailedFiles}");
                    Dispatcher.BeginInvoke(() => UpdateLiveErrorTable(), DispatcherPriority.Background);

                    if (processed % 200 == 0)
                    {
                        var metrics = BuildLiveSourceMetrics();
                        if (!string.IsNullOrWhiteSpace(metrics))
                            Log($"⚡ Métricas: {metrics}");
                    }
                }
            }

            bool RegisterCpdlLoadOutcome(bool hasFiles, out double currentRatio, out int currentNoFiles)
            {
                var total = System.Threading.Interlocked.Increment(ref cpdlTotalProcessed);
                lock (cpdlWindowLock)
                {
                    if (!hasFiles) cpdlNoFilesInWindow++;
                    cpdlWindow.Enqueue(hasFiles);
                    if (cpdlWindow.Count > cpdlWindowSize)
                    {
                        var evicted = cpdlWindow.Dequeue();
                        if (!evicted) cpdlNoFilesInWindow--;
                    }

                    currentNoFiles = cpdlNoFilesInWindow;
                    currentRatio = cpdlWindow.Count > 0 ? (double)currentNoFiles / cpdlWindow.Count : 0;

                    // #3: grace period — no evaluar auto-stop hasta N obras procesadas
                    if (total < cpdlGracePeriod)
                        return false;

                    if (cpdlWindow.Count < cpdlWindowMin)
                        return false;

                    // #9: umbral dual: ratio alto Y mínimo absoluto de sin-archivos en ventana
                    return currentRatio >= cpdlNoFilesStopRatio && currentNoFiles >= cpdlNoFilesStopAbsMin;
                }
            }

            static int GetSourceParallelism(string src) => src switch
            {
                "Mutopia" => 6,
                "CPDL" => 2,    // #5: reducido para menos 403/bloqueos
                _ => 4
            };

            static string NormalizeUrlForSort(string? url) =>
                string.IsNullOrWhiteSpace(url)
                    ? string.Empty
                    : url.Trim().ToLowerInvariant();

            static IEnumerable<PartituraFile> OrderFilesForSpeed(string source, IEnumerable<PartituraFile> files)
            {
                var materialized = files.ToList();
                if (materialized.Count == 0)
                    return materialized;

                return materialized
                    .OrderByDescending(f => string.Equals(f.Format, "PDF", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(f => f.Format, StringComparer.OrdinalIgnoreCase)
                    .GroupBy(f => NormalizeUrlForSort(f.DownloadUrl), StringComparer.Ordinal)
                    .Select(g => g.First());
            }

            var bySourceParallelism = downloadable
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Source) ? "Desconocida" : i.Source)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{g.Key}={GetSourceParallelism(g.Key)}")
                .ToArray();

            Log($"📥 Fase 2: descargando {downloadable.Count} obras ({string.Join(" | ", bySourceParallelism)})...");

            var sourceWorkers = downloadable
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Source) ? "Desconocida" : i.Source)
                .Select(async group =>
                {
                    var sourceName = group.Key;
                    var sourceItems = group.ToList();
                    var maxParallel = GetSourceParallelism(sourceName);
                    var minParallel = sourceName == "CPDL" ? 1 : 2;
                    var currentParallel = maxParallel;

                    var sourceStatsEntry = GetStats(sourceStats, sourceName);
                    sourceStatsEntry.CurrentParallelism = currentParallel;
                    sourceStatsEntry.TotalItems = sourceItems.Count;

                    for (int offset = 0; offset < sourceItems.Count;)
                    {
                        ct.ThrowIfCancellationRequested();

                        var batchSize = Math.Min(sourceItems.Count - offset, Math.Max(currentParallel * 24, currentParallel));
                        var batch = sourceItems.GetRange(offset, batchSize);
                        offset += batchSize;

                        int batchProcessed = 0;
                        int batchHardErrors = 0;

                        var options = new ParallelOptions
                        {
                            CancellationToken = ct,
                            MaxDegreeOfParallelism = currentParallel
                        };

                        await Parallel.ForEachAsync(batch, options, async (item, itemCt) =>
                        {
                            itemCt.ThrowIfCancellationRequested();

                            var stats = GetStats(sourceStats, item.Source);
                            System.Threading.Interlocked.Increment(ref stats.ItemsSeen);

                            if (item.Source == "CPDL" && System.Threading.Volatile.Read(ref cpdlAutoStopActive) == 1)
                            {
                                System.Threading.Interlocked.Increment(ref stats.CancelledItems);
                                System.Threading.Interlocked.Increment(ref totalCancelledItems);
                                System.Threading.Interlocked.Increment(ref totalAutoStoppedItems);
                                System.Threading.Interlocked.Increment(ref batchProcessed);
                                ReportProgressEvery50(silent: true);
                                // Evita tormenta de logs cuando CPDL entra en auto-stop.
                                // Estas obras se omiten de forma masiva y no aportan progreso útil por-item.
                                return;
                            }

                            if (item.Files.Count == 0)
                            {
                                bool loaded = false;
                                int loadAttempts = 0;
                                const int maxLoadAttempts = 2;

                                while (loadAttempts < maxLoadAttempts && !loaded)
                                {
                                    try
                                    {
                                        using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(itemCt);
                                        loadCts.CancelAfter(TimeSpan.FromSeconds(25));

                                        loaded = await _cpdl.LoadFilesAsync(item, loadCts.Token).ConfigureAwait(false);

                                        if (loaded)
                                            System.Threading.Volatile.Write(ref consecutiveErrors, 0);
                                    }
                                    catch (OperationCanceledException) when (!itemCt.IsCancellationRequested)
                                    {
                                        // Timeout por obra: seguir con siguiente elemento para que el lote avance.
                                        loaded = false;
                                    }
                                    catch (OperationCanceledException) { throw; }
                                    catch { }

                                    if (!loaded)
                                    {
                                        loadAttempts++;
                                        if (loadAttempts < maxLoadAttempts)
                                        {
                                            var backoffMs = 1500 + Random.Shared.Next(0, 500);
                                            await Task.Delay(backoffMs, itemCt).ConfigureAwait(false);
                                        }
                                    }
                                }

                                if (item.Source == "CPDL")
                                {
                                    var shouldStop = RegisterCpdlLoadOutcome(loaded, out var cRatio, out var cNoFiles);
                                    if (shouldStop && System.Threading.Interlocked.CompareExchange(ref cpdlAutoStopActive, 1, 0) == 0)
                                        progress.Report($"⚠️ CPDL auto-stop: ratio={cRatio:P0} sin-archivos={cNoFiles}/{cpdlWindowSize} en ventana; se omiten pendientes CPDL");
                                }

                                if (!loaded)
                                {
                                    System.Threading.Interlocked.Increment(ref consecutiveErrors);
                                    System.Threading.Interlocked.Increment(ref stats.RejectedItems);
                                    System.Threading.Interlocked.Increment(ref totalRejectedItems);
                                    AddErrorType(errorTypes, "Sin archivos detectados en la página");
                                    System.Threading.Interlocked.Increment(ref batchProcessed);
                                    ReportProgressEvery50();
                                    return;
                                }
                            }

                            var subFolder = BuildDestSubFolder(destFolder!, item);
                            bool itemDownloaded = false;
                            bool itemSkippedOnly = false;
                            bool itemHasError = false;
                            bool itemCancelled = false;
                            bool sawAnyFileResult = false;

                            foreach (var file in OrderFilesForSpeed(item.Source, item.Files))
                            {
                                itemCt.ThrowIfCancellationRequested();

                                try
                                {
                                    var (cookieHeader, userAgent) = item.Source switch
                                    {
                                        "CPDL" => _cpdl.GetSessionHeaders(),
                                        "Musopen" => _musopen.GetSessionHeaders(),
                                        _ => ((string?)null, (string?)null)
                                    };
                                    var result = await _downloader.DownloadFileAsync(file, subFolder, null, null, itemCt, cookieHeader, userAgent).ConfigureAwait(false);
                                    if (result.Success)
                                    {
                                        sawAnyFileResult = true;
                                        if (result.Skipped)
                                        {
                                            itemSkippedOnly = true;
                                            System.Threading.Interlocked.Increment(ref stats.SkippedFiles);
                                            System.Threading.Interlocked.Increment(ref totalSkippedFiles);
                                        }
                                        else
                                        {
                                            itemDownloaded = true;
                                            itemSkippedOnly = false;
                                            System.Threading.Interlocked.Increment(ref stats.DownloadedFiles);
                                            System.Threading.Interlocked.Add(ref stats.DownloadedBytes, result.BytesDownloaded);
                                            System.Threading.Interlocked.Increment(ref totalDownloadedFiles);
                                        }
                                        System.Threading.Volatile.Write(ref consecutiveErrors, 0);
                                    }
                                    else if (result.Cancelled)
                                    {
                                        sawAnyFileResult = true;
                                        itemCancelled = true;
                                        System.Threading.Interlocked.Increment(ref stats.CancelledFiles);
                                        System.Threading.Interlocked.Increment(ref totalCancelledFiles);
                                    }
                                    else
                                    {
                                        sawAnyFileResult = true;
                                        itemHasError = true;
                                        System.Threading.Interlocked.Increment(ref stats.FailedFiles);
                                        System.Threading.Interlocked.Increment(ref totalFailedFiles);
                                        AddErrorType(errorTypes, result.Error);
                                        var errors = System.Threading.Interlocked.Increment(ref consecutiveErrors);
                                        if (errors >= 5)
                                            await Task.Delay(1200, itemCt).ConfigureAwait(false);
                                    }
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception ex)
                                {
                                    sawAnyFileResult = true;
                                    itemHasError = true;
                                    System.Threading.Interlocked.Increment(ref stats.FailedFiles);
                                    System.Threading.Interlocked.Increment(ref totalFailedFiles);
                                    AddErrorType(errorTypes, ex.GetType().Name);

                                    var errors = System.Threading.Interlocked.Increment(ref consecutiveErrors);
                                    if (errors >= 5)
                                        await Task.Delay(1200, itemCt).ConfigureAwait(false);
                                }
                            }

                            if (itemHasError)
                            {
                                System.Threading.Interlocked.Increment(ref stats.ErroredItems);
                                System.Threading.Interlocked.Increment(ref totalErroredItems);
                                System.Threading.Interlocked.Increment(ref batchHardErrors);
                            }
                            else if (itemCancelled)
                            {
                                System.Threading.Interlocked.Increment(ref stats.CancelledItems);
                                System.Threading.Interlocked.Increment(ref totalCancelledItems);
                            }
                            else if (itemDownloaded)
                            {
                                System.Threading.Interlocked.Increment(ref stats.DownloadedItems);
                                System.Threading.Interlocked.Increment(ref totalDownloadedItems);
                            }
                            else if (itemSkippedOnly || sawAnyFileResult)
                            {
                                System.Threading.Interlocked.Increment(ref stats.ExistingItems);
                                System.Threading.Interlocked.Increment(ref totalExistingItems);
                            }

                            System.Threading.Interlocked.Increment(ref batchProcessed);
                            ReportProgressEvery50();
                        }).ConfigureAwait(false);

                        if (batchProcessed >= Math.Max(12, currentParallel * 4))
                        {
                            var hardRatio = batchProcessed == 0 ? 0 : (double)batchHardErrors / batchProcessed;
                            var previous = currentParallel;

                            if (hardRatio >= 0.18 && currentParallel > minParallel)
                                currentParallel--;
                            else if (hardRatio <= 0.04 && currentParallel < maxParallel)
                                currentParallel++;

                            if (currentParallel != previous)
                            {
                                sourceStatsEntry.CurrentParallelism = currentParallel;
                                progress.Report($"⚙️ {sourceName}: concurrencia {previous} -> {currentParallel} (error duro {hardRatio:P0})");
                            }
                        }
                    }
                })
                .ToList();

            await Task.WhenAll(sourceWorkers).ConfigureAwait(false);

            LogDebug("Resumen detallado por fuente:");
            foreach (var kv in sourceStats.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var s = kv.Value;
                LogDebug($"   {kv.Key}: vistas={s.ItemsSeen} obras ✅nuevas={s.DownloadedItems} ⏭️previas={s.ExistingItems} 🚫sin-archivos={s.RejectedItems} ❌error={s.ErroredItems} 🛑canceladas={s.CancelledItems} · archivos ✅{s.DownloadedFiles} ⏭️{s.SkippedFiles} ❌{s.FailedFiles}");
            }

            if (!errorTypes.IsEmpty)
            {
                Log("📉 Tipos de error:");
                foreach (var kv in errorTypes.OrderByDescending(k => k.Value).Take(8))
                    Log($"   {kv.Key}: {kv.Value}");
            }

            Log($"📌 Totales obras: Nuevas={totalDownloadedItems} | Ya en carpeta={totalExistingItems} | Sin archivos detectados={totalRejectedItems} | Error={totalErroredItems} | Canceladas={totalCancelledItems}");
            Log($"📌 Totales archivos: Nuevos={totalDownloadedFiles} | Ya en carpeta={totalSkippedFiles} | Error={totalFailedFiles} | Cancelados={totalCancelledFiles}");
            if (totalAutoStoppedItems > 0)
                Log($"🛑 Auto-stop: {totalAutoStoppedItems} obras omitidas por presión sostenida en fuentes protegidas (CPDL)");

            var degraded = totalAutoStoppedItems > 0;
            progress.Report($"{(degraded ? "⚠️ Completado con degradación" : "✅ Completado")}: obras {totalProcessedItems}/{downloadable.Count} · nuevas={totalDownloadedItems} previas={totalExistingItems} sin-archivos={totalRejectedItems} error={totalErroredItems} · archivos ✅{totalDownloadedFiles} ⏭️{totalSkippedFiles} ❌{totalFailedFiles}");
            ShowToast("ScoreDown", degraded
                ? $"Completado con degradación: auto-stop {totalAutoStoppedItems} obras"
                : $"Catálogo completo: obras nuevas {totalDownloadedItems} · previas {totalExistingItems}");
            _circuitBreaker?.RecordSuccess();
        }
        catch (OperationCanceledException)
        {
            SaveOfflineLibrary();
            Log($"⏹ Detenido: biblioteca guardada ({catalogItems.Count} obras catalogadas)");
        }
        catch (Exception ex)
        {
            Log($"❌ Error en catálogo: {ex.Message}");
            _circuitBreaker?.RecordError();
            Log($"   Circuit breaker: {_circuitBreaker?.GetStatus()}");
        }
        finally
        {
            _cpdl.FlushNoFilesBlacklist();
            btnFetchCatalog.IsEnabled = true;
            btnDownloadAll.IsEnabled = true;
            btnCancelCatalog.IsEnabled = false;
            btnCancelAllDownloads.IsEnabled = false;
        }
    }

    private void BtnCancelAllDownloads_Click(object sender, RoutedEventArgs e)
    {
        _catalogCts?.Cancel();
        _downloadCts?.Cancel();
        Log("⏹ Descargas canceladas por el usuario");
    }

    private async Task<List<PartituraItem>> ApplyBulkPreflightAsync(
        List<PartituraItem> downloadable,
        IProgress<string> progress,
        CancellationToken ct)
    {
        if (downloadable.Count < 100)
            return downloadable;

        const int sampleSize = 100;
        const int minSample = 40;

        var skipSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = downloadable
            .Where(i => i.Files.Count == 0 && i.Source == "CPDL")
            .GroupBy(i => i.Source)
            .ToList();

        foreach (var group in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var source = group.Key;
            var sample = group.Take(sampleSize).ToList();
            if (sample.Count == 0)
                continue;

            progress.Report($"🧪 Preflight {source}: muestreando {sample.Count} obras...");

            var tested = 0;
            var withFiles = 0;
            foreach (var item in sample)
            {
                ct.ThrowIfCancellationRequested();

                bool loaded;
                try
                {
                    loaded = await _cpdl.LoadFilesAsync(item, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    loaded = false;
                }

                tested++;
                if (loaded && item.Files.Count > 0)
                    withFiles++;
            }

            var ratio = tested == 0 ? 0 : (double)withFiles / tested;
            var threshold = 0.35;
            LogDebug($"Preflight {source}: {withFiles}/{tested} con archivos ({ratio:P0})");

            if (tested >= minSample && ratio < threshold)
            {
                skipSources.Add(source);
                LogDebug($"Preflight {source}: tasa baja ({ratio:P0}) — se excluye de descarga masiva en esta corrida");
            }
        }

        if (skipSources.Count == 0)
            return downloadable;

        var filtered = downloadable.Where(i => !skipSources.Contains(i.Source)).ToList();
        progress.Report($"⚠️ Preflight activo: fuentes omitidas => {string.Join(", ", skipSources.OrderBy(x => x))}");
        return filtered;
    }

    private void BtnCpdlSession_Click(object sender, RoutedEventArgs e)
    {
        Forget(OpenCpdlSessionAsync(), "sesión CPDL");
    }

    private void BtnMusopenSession_Click(object sender, RoutedEventArgs e)
    {
        Forget(OpenMusopenSessionAsync(), "sesión Musopen");
    }

    private Task<bool> OpenMusopenSessionAsync()
    {
        return Dispatcher.InvokeAsync<bool>(() =>
        {
            try
            {
                var dlg = new CpdlSessionDialog(
                    "Musopen",
                    "https://musopen.org/accounts/login/",
                    ["https://musopen.org/", "https://dl.musopen.org/"],
                    requiredCookieNames: null,
                    allowFallbackWithoutRequiredCookies: true)
                { Owner = this };

                var ok = dlg.ShowDialog();
                if (ok == true && !string.IsNullOrWhiteSpace(dlg.CookieHeader))
                {
                    _musopen.SetSession(dlg.CookieHeader, dlg.UserAgent);
                    SaveUiState();  // persistir sesión para próximo arranque
                    Log("🔐 Musopen sesión guardada. Las descargas usarán tu cuenta.");
                    txtStatus.Text = "Musopen: sesión activa";
                    UpdateSourceDashboard();
                    return true;
                }
                Log("ℹ️ Musopen sesión cancelada.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en sesión Musopen: {ex.Message}");
                return false;
            }
        }).Task;
    }

    private void Forget(Task task, string context)
    {
        _ = task.ContinueWith(
            t => Log($"❌ Error async en {context}: {t.Exception?.GetBaseException().Message}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private Task<bool> OpenCpdlSessionAsync()
    {
        return Dispatcher.InvokeAsync<bool>(() =>
        {
            try
            {
                var dlg = new CpdlSessionDialog { Owner = this };
                var ok = dlg.ShowDialog();
                if (ok == true && !string.IsNullOrWhiteSpace(dlg.CookieHeader))
                {
                    _cpdl.SetManualSession(dlg.CookieHeader, dlg.UserAgent);
                    // Crear/reemplazar sesión WebView2 — mismo perfil = mismo TLS fingerprint que resolvió el challenge.
                    _cpdlWebSession?.Dispose();
                    _cpdlWebSession = new Infrastructure.CpdlWebSession();
                    _cpdl.WebViewFetchAsync = _cpdlWebSession.FetchAsync;

                    Log("🔐 CPDL sesión interactiva guardada. Reintentando...");
                    txtStatus.Text = "CPDL sesión activa (WebView2)";
                    return true;
                }
                Log("ℹ️ CPDL sesión cancelada o sin cookies válidas.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en sesión CPDL: {ex.Message}");
                return false;
            }
        }).Task;
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e) => RequestAppShutdown();

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(txtLog.Text))
            System.Windows.Clipboard.SetText(txtLog.Text);
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => txtLog.Clear();

    private static string BuildDestSubFolder(string destFolder, PartituraItem item) =>
        Path.Combine(destFolder,
            DownloadService.SanitizeFolderNameStatic(
                string.IsNullOrWhiteSpace(item.Composer) ? "Varios" : item.Composer));

    private async Task DoSearchAsync()
    {
        var query = BuildSearchQuery();
        if (string.IsNullOrEmpty(query)) return;
        AddToSearchHistory(query);
        _sessionSearches++;
        UpdateSessionStats();
        popComposerSuggestions.IsOpen = false;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        SetSearchRunning(true);
        ClearResults();
        Log($"🔍 Buscando: {query}");

        try
        {
            IProgress<string> progress = new Progress<string>(msg => { txtStatus.Text = msg; Log(msg); });
            var source = (cmbSource.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Mutopia";

            PurgeExpiredCache();  // Limpia cache expirado antes de nueva búsqueda

            List<PartituraItem> results = [];

            if (source == "Mutopia")
            {
                results = await FetchWithCacheAsync("Mutopia", query,
                    (p, token) => _mutopia.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "CPDL")
            {
                results = await FetchWithCacheAsync("CPDL", query,
                    (p, token) => _cpdl.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "Musopen")
            {
                results = await FetchWithCacheAsync("Musopen", query,
                    (p, token) => _musopen.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "Offline")
            {
                progress.Report("🔍 Buscando en biblioteca offline...");
                results = CloneItems(_offlineLibraryItems)
                    .Where(i => i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || i.Composer.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || i.UserTag.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                progress.Report($"✅ {results.Count} obras encontradas offline");
            }
            else // Todas
            {
                async Task<List<PartituraItem>> SafeSearchAsync(string name, Func<Task<List<PartituraItem>>> run)
                {
                    try
                    {
                        return await run();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ {name} falló: {ex.Message}");
                        return [];
                    }
                }

                progress.Report("🔍 Buscando en Mutopia Project...");
                var mutopiaTask = SafeSearchAsync("Mutopia", () =>
                    FetchWithCacheAsync("Mutopia", query, (p, token) => _mutopia.SearchAsync(query, p, token), null, ct));
                progress.Report("🔍 Buscando en CPDL...");
                var cpdlTask = SafeSearchAsync("CPDL", () =>
                    FetchWithCacheAsync("CPDL", query, (p, token) => _cpdl.SearchAsync(query, p, token), null, ct));
                var musopenTask = _enableMusopen && _musopen.HasApiKey
                    ? SafeSearchAsync("Musopen", () =>
                        FetchWithCacheAsync("Musopen", query, (p, token) => _musopen.SearchAsync(query, p, token), null, ct))
                    : Task.FromResult<List<PartituraItem>>([]);

                var all = await Task.WhenAll(mutopiaTask, cpdlTask, musopenTask);
                results.AddRange(all[0]);
                results.AddRange(all[1]);
                results.AddRange(all[2]);
            }

            foreach (var item in results)
                _allResults.Add(item);

            // Evita duplicados al combinar fuentes
            var deduped = _allResults
                .GroupBy(BuildDedupKey)
                .Select(g => g
                    .OrderByDescending(i => i.Files.Count)
                    .ThenByDescending(i => i.Files.Sum(f => f.SizeBytes))
                    .ThenByDescending(i => i.Files.Select(f => NormalizeUrl(f.DownloadUrl)).Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    .First())
                .ToList();

            var ranked = deduped
                .OrderByDescending(i => ComputeRelevance(i, query))
                .ThenByDescending(i => i.Files.Sum(f => f.SizeBytes))
                .ThenBy(i => i.Title)
                .ToList();

            _allResults.Clear();
            foreach (var item in ranked)
            {
                ApplySavedTag(item);
                _allResults.Add(item);
            }

            ApplyFilter();
            txtResultCount.Text = $"{_filtered.Count} obras";
            btnDownload.IsEnabled = _filtered.Count > 0;
            _sessionResults += _filtered.Count;
            UpdateSessionStats();
            // Puebla cache de compositores para autocompletado — O(N) una vez, no por tecla
            foreach (var item in _allResults)
                if (!string.IsNullOrWhiteSpace(item.Composer))
                    _knownComposersCache.Add(item.Composer);
            Log($"✅ {_filtered.Count} obras encontradas");
        }
        catch (OperationCanceledException)
        {
            txtStatus.Text = "Búsqueda cancelada.";
            Log("⚠️ Búsqueda cancelada");
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"❌ Error: {ex.Message}";
            Log($"❌ Error en búsqueda: {ex.Message}");
        }
        finally
        {
            SetSearchRunning(false);
        }
    }

    private void SetSearchRunning(bool running)
    {
        btnSearch.IsEnabled = !running;
        btnCancelSearch.IsEnabled = running;
        txtStatus.Text = running ? "Buscando..." : "Listo.";
    }

    // ── Filtros ───────────────────────────────────────────────────────────

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        ApplyFilter();
        SaveUiState();
    }

    private void CmbHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        if (cmbHistory.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            txtSearch.Text = selected;
    }

    private void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        SaveUiState();
    }

    private void UpdateSourceDashboard()
    {
        if (!IsInitialized || txtSourceStatus is null) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateSourceDashboard, DispatcherPriority.Background);
            return;
        }
        var m = _enableMutopia ? "ON" : "OFF";
        var c = _enableCpdl ? "ON" : "OFF";
        var mu = _enableMusopen ? (_musopen.HasSession ? "ON+🔐" : "ON") : "OFF";
        txtSourceStatus.Text = $"Fuentes M:{m} C:{c} MO:{mu}";
    }

    private static List<PartituraItem> MergePendingLists(IEnumerable<PartituraItem> a, IEnumerable<PartituraItem> b)
    {
        var map = new Dictionary<string, PartituraItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in a.Concat(b))
        {
            var key = string.IsNullOrWhiteSpace(item.PageUrl)
                ? $"{item.Source}|{item.Title}|{item.Composer}|{item.Files.Count}"  // clave determinista sin depender del índice
                : item.PageUrl;
            if (!map.ContainsKey(key))
                map[key] = item;
        }
        return map.Values.ToList();
    }

    private void UpdateLiveErrorTable()
    {
        if (txtErrorMiniTable is null) return;
        if (_liveErrorTypes.IsEmpty)
        {
            txtErrorMiniTable.Text = string.Empty;
            return;
        }
        var top = _liveErrorTypes.OrderByDescending(kv => kv.Value).Take(5);
        txtErrorMiniTable.Text = "❌ " + string.Join(" · ", top.Select(kv =>
        {
            // compactar clave larga en etiqueta corta
            var label = kv.Key.Length > 20 ? kv.Key[..20] + "…" : kv.Key;
            return $"{label}:{kv.Value}";
        }));
    }

    private void SourceToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        _enableMutopia = chkEnableMutopia.IsChecked == true;
        _enableCpdl = chkEnableCpdl.IsChecked == true;
        _enableMusopen = chkEnableMusopen.IsChecked == true;

        // Mantener al menos una fuente activa para evitar corridas vacías.
        if (!_enableMutopia && !_enableCpdl && !_enableMusopen)
        {
            _enableMutopia = true;
            chkEnableMutopia.IsChecked = true;
            Log("ℹ️ Debe quedar al menos una fuente activa; Mutopia reactivado.");
        }

        btnCpdlSession.IsEnabled = _enableCpdl;
        if (!_enableCpdl)
            _cpdl.SetManualSession(null, null);

        SaveUiState();
        UpdateSourceDashboard();
    }

    private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        bool sel = chkSelectAll.IsChecked == true;
        foreach (var item in _filtered)
            item.IsSelected = sel;   // R4: INotifyPropertyChanged fires automatically
        UpdateDownloadButton();
    }

    private void ApplyFilter()
    {
        bool onlyPdf = chkOnlyPdf.IsChecked == true;
        bool onlyMidi = chkOnlyMidi.IsChecked == true;
        var tagFilter = txtTagFilter.Text?.Trim() ?? string.Empty;
        var titleQuery = txtTitleSearch.Text?.Trim() ?? string.Empty;
        var composerQuery = txtComposerSearch.Text?.Trim() ?? string.Empty;
        var sourceFilter = (cmbFilterSource.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todas";

        // R8: use ICollectionView filter — no ItemsSource reassignment, scroll/selection preserved
        Predicate<object> filterPredicate = obj =>
        {
            if (obj is not PartituraItem item) return false;
            if (!string.Equals(sourceFilter, "Todas", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Source, sourceFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(tagFilter) && !item.UserTag.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(titleQuery) && !item.Title.Contains(titleQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(composerQuery) && !item.Composer.Contains(composerQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (onlyPdf || onlyMidi)
            {
                var hasPdf = item.Files.Any(f => string.Equals(f.Format, "PDF", StringComparison.OrdinalIgnoreCase));
                var hasMidi = item.Files.Any(f => string.Equals(f.Format, "MIDI", StringComparison.OrdinalIgnoreCase));
                var formatOk = onlyPdf && onlyMidi ? (hasPdf || hasMidi)
                    : onlyPdf ? hasPdf
                    : hasMidi;
                if (!formatOk) return false;
            }
            return true;
        };
        _resultsView.View.Filter = filterPredicate;

        _filtered = _allResults.Where(i => filterPredicate(i)).ToList();
        txtResultCount.Text = $"{_filtered.Count} obras";
        UpdateDownloadButton();
    }

    private string BuildSearchQuery()
    {
        // R1: txtTagFilter is a local post-search filter, NOT a network keyword.
        var general = txtSearch.Text?.Trim() ?? string.Empty;
        var title = txtTitleSearch.Text?.Trim() ?? string.Empty;
        var composer = txtComposerSearch.Text?.Trim() ?? string.Empty;
        return string.Join(' ', new[] { general, title, composer }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
    }

    private void TxtTagFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized) return;
        // R3: debounce — restart timer on each keystroke, fire ApplyFilter once idle 250 ms
        _tagFilterDebounce.Stop();
        _tagFilterDebounce.Start();
    }

    private void BtnBulkTag_Click(object sender, RoutedEventArgs e)
    {
        var selected = _filtered.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            DarkDialogService.ShowMessage(this, "Marca al menos una obra para edición masiva.", "Sin selección", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newTag = txtTagEditor.Text?.Trim() ?? string.Empty;
        foreach (var item in selected)
        {
            item.UserTag = newTag;  // R4: INotifyPropertyChanged propagates to badge in template
            var key = BuildTagKey(item);
            var legacyKey = BuildDedupKey(item);
            if (string.IsNullOrWhiteSpace(newTag))
            {
                _savedTags.Remove(key);
                _savedTags.Remove(legacyKey);
            }
            else
            {
                _savedTags[key] = newTag;
                _savedTags.Remove(legacyKey);
            }
        }
        SaveTags();
        // R4: no lstResults.Items.Refresh() needed — INPC handles tag badge update
        ApplyFilter();
        txtTagStatus.Text = $"Tag aplicado a {selected.Count} obra(s)";
    }

    private void UpdateDownloadButton()
    {
        bool hasSelection = _filtered.Any(i => i.IsSelected);
        btnDownload.IsEnabled = hasSelection;
        bool hasResults = _filtered.Count > 0;
        btnExportJson.IsEnabled = hasResults;
        btnExportCsv.IsEnabled = hasResults;
    }

    private void ItemSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateDownloadButton();
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        _searchCache.Clear();
        _cacheHits = 0;
        _cacheMisses = 0;
        UpdateCacheStats();
        Log("🧹 Cache limpiada");
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_filtered.Count == 0) return;
        var exportItems = GetExportItems();
        if (exportItems.Count == 0) return;
        var outputPath = DarkDialogService.PromptSaveFile(
            this,
            "Exportar resultados a JSON",
            Path.Combine(txtDestFolder.Text?.Trim() ?? string.Empty, $"partituras_{DateTime.Now:yyyyMMdd_HHmm}.json"),
            ".json");
        if (string.IsNullOrWhiteSpace(outputPath)) return;
        try
        {
            var data = exportItems.Select(i => new
            {
                i.Source,
                i.Composer,
                i.Title,
                i.PageUrl,
                i.UserTag,
                Files = i.Files.Select(f => new { f.Format, f.DownloadUrl, f.FileName, f.SizeBytes })
            });
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json, System.Text.Encoding.UTF8);
            Log($"📄 JSON exportado: {outputPath} ({exportItems.Count} obras)");
        }
        catch (Exception ex)
        {
            DarkDialogService.ShowMessage(this, $"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_filtered.Count == 0) return;
        var exportItems = GetExportItems();
        if (exportItems.Count == 0) return;
        var outputPath = DarkDialogService.PromptSaveFile(
            this,
            "Exportar resultados a CSV",
            Path.Combine(txtDestFolder.Text?.Trim() ?? string.Empty, $"partituras_{DateTime.Now:yyyyMMdd_HHmm}.csv"),
            ".csv");
        if (string.IsNullOrWhiteSpace(outputPath)) return;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Fuente,Compositor,Título,Página,Tag,Formato,Archivo,URL,TamañoBytes");
            foreach (var item in exportItems)
            {
                if (item.Files.Count == 0)
                {
                    sb.AppendLine($"{CsvEscape(item.Source)},{CsvEscape(item.Composer)},{CsvEscape(item.Title)},{CsvEscape(item.PageUrl)},{CsvEscape(item.UserTag)},,,, ");
                }
                else
                {
                    foreach (var f in item.Files)
                        sb.AppendLine($"{CsvEscape(item.Source)},{CsvEscape(item.Composer)},{CsvEscape(item.Title)},{CsvEscape(item.PageUrl)},{CsvEscape(item.UserTag)},{CsvEscape(f.Format)},{CsvEscape(f.FileName)},{CsvEscape(f.DownloadUrl)},{f.SizeBytes}");
                }
            }
            File.WriteAllText(outputPath, sb.ToString(), System.Text.Encoding.UTF8);
            Log($"📋 CSV exportado: {outputPath} ({exportItems.Count} obras)");
        }
        catch (Exception ex)
        {
            DarkDialogService.ShowMessage(this, $"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<PartituraItem> GetExportItems()
    {
        var selected = _filtered.Where(i => i.IsSelected).ToList();
        if (selected.Count > 0 && selected.Count < _filtered.Count)
        {
            var choice = DarkDialogService.ShowMessage(
                this,
                $"Hay {selected.Count} obra(s) marcadas de {_filtered.Count}.\n\nSí: exportar marcadas\nNo: exportar todas las filtradas",
                "Exportar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return [];
            return choice == MessageBoxResult.Yes ? selected : _filtered;
        }

        return _filtered;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ── Descarga ──────────────────────────────────────────────────────────

    private void BtnChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = DarkDialogService.PromptFolder(this, "Carpeta de destino para las partituras", txtDestFolder.Text);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            txtDestFolder.Text = folder;
            _currentDestFolder = folder;
            SaveUiState();
        }
    }

    private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
    }

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        // Guard: evitar dos descargas simultáneas (auto-batch + click manual)
        if (_downloadCts is not null && !_downloadCts.IsCancellationRequested)
        {
            Log("⚠️ Descarga ya en curso, solicitud ignorada");
            return;
        }

        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrEmpty(destFolder))
        {
            DarkDialogService.ShowMessage(this, "Elige una carpeta de destino.", "Faltan datos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Construir lista de trabajos: todas las obras seleccionadas, todos sus archivos
        bool onlyPdf = chkOnlyPdf.IsChecked == true;
        bool onlyMidi = chkOnlyMidi.IsChecked == true;

        var selectedItems = _filtered.Where(i => i.IsSelected).ToList();
        var missingFilesItems = selectedItems
            .Where(i => i.Files.Count == 0
                     && string.Equals(i.Source, "CPDL", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (missingFilesItems.Count > 0)
        {
            Log($"🔄 Cargando enlaces de archivo para {missingFilesItems.Count} obras seleccionadas...");
            txtStatus.Text = $"Preparando descargas ({missingFilesItems.Count} obras sin enlaces)...";

            int loadedOk = 0, loadedFailed = 0;
            bool hadTimeout = false;
            using var warmupCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            foreach (var item in missingFilesItems)
            {
                try
                {
                    if (warmupCts.Token.IsCancellationRequested)
                        throw new OperationCanceledException();

                    await _cpdl.LoadFilesAsync(item, warmupCts.Token);

                    loadedOk++;
                }
                catch (OperationCanceledException)
                {
                    loadedFailed = missingFilesItems.Count - loadedOk;
                    hadTimeout = true;
                    LogDebug($"Timeout cargando metadata: {loadedOk}/{missingFilesItems.Count} OK, {loadedFailed} omitidos");
                    break;
                }
                catch (Exception ex)
                {
                    LogDebug($"Error cargando {item.Title} ({item.Source}): {ex.Message}");
                    loadedFailed++;
                }
            }
            if (loadedFailed > 0 && hadTimeout)
                Log($"⚠️ Carga de metadatos timeout después de {loadedOk}/{missingFilesItems.Count} obras");
        }

        var jobs = selectedItems
            .SelectMany(i => i.Files
                .Where(f =>
                {
                    if (!onlyPdf && !onlyMidi) return true;
                    var isPdf = string.Equals(f.Format, "PDF", StringComparison.OrdinalIgnoreCase);
                    var isMidi = string.Equals(f.Format, "MIDI", StringComparison.OrdinalIgnoreCase);
                    return onlyPdf && onlyMidi ? (isPdf || isMidi)
                        : onlyPdf ? isPdf
                        : isMidi;
                })
                .Select(f => (item: i, file: f)))
            .ToList();

        if (jobs.Count == 0)
        {
            DarkDialogService.ShowMessage(this, "No hay archivos que descargar con los filtros actuales.", "Sin archivos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        PrepareQueue(jobs);

        _currentDestFolder = destFolder;
        SaveUiState();   // persist chosen folder immediately
        SetDownloadRunning(true);
        pbProgress.Value = 0;
        pbFileProgress.Visibility = Visibility.Visible;
        txtFileProgress.Visibility = Visibility.Visible;
        pbFileProgress.Value = 0;
        Log($"⬇️ Iniciando descarga de {jobs.Count} archivo(s) en {destFolder}");

        var batchSw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var progress = new Progress<(string message, double percent, int done, int total)>(r =>
            {
                var eta = r.percent > 5 ? $"  ·  ETA {FormatEta(batchSw.Elapsed, r.percent)}" : string.Empty;
                txtStatus.Text = r.message + eta;
                pbProgress.Value = r.percent;
                if (r.done == r.total) batchSw.Stop();
                else LogDebug(r.message);
            });

            var fileProgress = new Progress<(PartituraFile file, string state, double filePct, double speedKBps)>(r =>
            {
                txtFileProgress.Text = $"↳ {r.file.FileName} · {r.speedKBps:F0} KB/s";
                pbFileProgress.Value = r.filePct;
                UpdateQueue(r.file, r.state, r.filePct, r.speedKBps);
                if (r.state.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                {
                    var msg = r.state.Length > 6 ? r.state[6..].Trim() : "(desconocido)";
                    Log($"   ❌ {r.file.FileName}: {msg}");
                }
            });

            var result = await _downloader.DownloadAllAsync(jobs, destFolder, progress, fileProgress,
                file => _pausedQueueFiles.ContainsKey(file.DownloadUrl),
                source => (source ?? string.Empty).Trim().ToUpperInvariant() switch
                {
                    "CPDL" => _cpdl.GetSessionHeaders(),
                    _ => (null, null)
                },
                ct);
            _sessionDownloads += result.Ok;
            _sessionBytes += result.BytesDownloaded;
            UpdateSessionStats();

            var summary = $"✅ Completado: {result.Ok} OK, {result.Skipped} ya existían, {result.Cancelled} cancelados, {result.Failed} fallidos";
            txtStatus.Text = summary;
            Log(summary);

            if (result.Ok > 0 || result.Skipped > 0)
            {
                // Toast notification visible aunque la ventana esté minimizada/en segundo plano.
                ShowToast("Descarga completada",
                    $"{result.Ok} archivo(s) descargado(s)" +
                    (result.Skipped > 0 ? $", {result.Skipped} ya existían" : string.Empty) +
                    (result.Failed > 0 ? $", {result.Failed} fallidos" : string.Empty),
                    result.Failed > 0 ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);

                var open = DarkDialogService.ShowMessage(this, $"{summary}\n\n¿Abrir carpeta de destino?",
                    "Descarga completada", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = destFolder,
                        UseShellExecute = true
                    });
            }

            if (chkAutoConvertAudiveris.IsChecked == true && result.Ok > 0 && !ct.IsCancellationRequested)
                await AutoConvertWithAudiverisAsync(destFolder);
        }
        catch (OperationCanceledException)
        {
            txtStatus.Text = "Descarga cancelada.";
            Log("⚠️ Descarga cancelada por el usuario");
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"❌ Error: {ex.Message}";
            Log($"❌ Error en descarga: {ex.Message}");
        }
        finally
        {
            SetDownloadRunning(false);
            pbProgress.Value = 0;
            pbFileProgress.Value = 0;
            pbFileProgress.Visibility = Visibility.Collapsed;
            txtFileProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void SetDownloadRunning(bool running)
    {
        btnDownload.IsEnabled = !running;
        btnCancelDownload.IsEnabled = running;
        btnSearch.IsEnabled = !running;
        if (btnConvertAudiveris != null)
            btnConvertAudiveris.IsEnabled = !running && !_audiverisRunning;
        if (btnGenerateVideo != null)
            btnGenerateVideo.IsEnabled = !running && !_videoRunning;
    }

    private async Task AutoConvertWithAudiverisAsync(string destFolder)
    {
        if (_audiverisRunning) return;

        var audiverisExe = ResolveAudiverisExecutable();
        if (string.IsNullOrWhiteSpace(audiverisExe))
        {
            Log("⚠️ Auto-convertir: Audiveris no encontrado. Define AUDIVERIS_EXE o instala Audiveris.");
            return;
        }

        var pendingBeforeCooldown = SafeEnumerateFiles(destFolder, f => AudiverisInputExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) && !HasMusicScoreSibling(f))
            .Where(f => !_audiverisKnownPageFailures.Contains(f))
            .ToList();
        var pending = pendingBeforeCooldown
            .Where(f => !IsAudiverisFamilyInTimeoutCooldown(f))
            .ToList();
        var skippedCooldownFamilies = pendingBeforeCooldown.Count - pending.Count;

        if (skippedCooldownFamilies > 0)
            Log($"ℹ️ Auto-Audiveris: {skippedCooldownFamilies} archivo(s) omitidos por cooldown de timeout activo.");

        if (pending.Count == 0)
        {
            Log("🎼 Auto-convertir: todo ya estaba convertido o no hay PDF/imagen.");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates);
        if (skippedFamilyDuplicates > 0)
            Log($"ℹ️ Auto-Audiveris: {skippedFamilyDuplicates} archivo(s) omitidos por duplicado de familia (a4/let).");

        Log($"🎼 Auto-convertir: procesando {familyPending.Count} archivo(s) con Audiveris...");
        _audiverisRunning = true;
        if (btnConvertAudiveris != null) btnConvertAudiveris.IsEnabled = false;

        int ok = 0, partial = 0, fail = 0, skippedByFamilyTimeout = 0, processed = 0;
        var timeoutFamilies = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await Parallel.ForEachAsync(
                familyPending,
                new ParallelOptions { MaxDegreeOfParallelism = _audiverisParallel },
                async (input, cancellationToken) =>
                {
                    var name = Path.GetFileName(input);
                    var familyKey = NormalizeAudiverisFamilyKey(input);
                    if (timeoutFamilies.ContainsKey(familyKey))
                    {
                        Interlocked.Increment(ref skippedByFamilyTimeout);
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⏭️ Omitido (familia timeout)" }));
                        return;
                    }

                    var idx = Interlocked.Increment(ref processed);
                    await Dispatcher.InvokeAsync(() =>
                        txtStatus.Text = $"🎼 Auto-Audiveris [{idx}/{familyPending.Count}] {name}");
                    LogDebug($"Auto-Audiveris [{idx}/{familyPending.Count}] {name}");

                    (bool Success, bool Partial, bool PageFailure) result;
                    try
                    {
                        result = await RunAudiverisConversionAsync(audiverisExe, input).ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        Interlocked.Increment(ref fail);
                        timeoutFamilies.TryAdd(familyKey, 0);
                        MarkAudiverisFamilyTimeout(input);
                        Log($"❌ Auto-Audiveris timeout en {name}: {tex.Message}");
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⏱️ Timeout" }));
                        return;
                    }

                    if (result.Success)
                    {
                        _audiverisTimeoutStrikes.TryRemove(familyKey, out _);
                        _audiverisStrikeLastUtc.TryRemove(familyKey, out _);
                        if (result.Partial)
                        {
                            Interlocked.Increment(ref partial);
                            Log($"⚠️ Conversión parcial: {name} (solo hoja 1)");
                            await Dispatcher.InvokeAsync(() =>
                                _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⚠️ Parcial (hoja 1)" }));
                        }
                        else
                        {
                            Interlocked.Increment(ref ok);
                            await Dispatcher.InvokeAsync(() =>
                                _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "✅ Convertido" }));
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref fail);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (result.PageFailure)
                            {
                                _audiverisKnownPageFailures.Add(input);
                            }
                            Log($"⚠️ Sin salida: {name}");
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⚠️ Sin salida" });
                        });
                    }
                }).ConfigureAwait(true);

            if (_audiverisKnownPageFailures.Count > 0)
                SaveAudiverisPageFailures();
            if (!_audiverisTimeoutFamilies.IsEmpty)
                SaveAudiverisTimeoutFamilies();
            if (!_audiverisTimeoutStrikes.IsEmpty)
                SaveAudiverisTimeoutStrikes();
            UpdateAudiverisStatus();
            var msg = $"🎼 Auto-Audiveris finalizado: {ok} completos, {partial} parciales, {fail} sin convertir" +
                      (skippedByFamilyTimeout > 0 ? $", {skippedByFamilyTimeout} omitidos por timeout de familia" : string.Empty);
            txtStatus.Text = msg;
            Log(msg);
        }
        catch (Exception ex)
        {
            Log($"❌ Auto-Audiveris error: {ex.Message}");
        }
        finally
        {
            _audiverisRunning = false;
            if (btnConvertAudiveris != null) btnConvertAudiveris.IsEnabled = true;
        }
    }

    private void BtnResetAudiverisCooldown_Click(object sender, RoutedEventArgs e)
    {
        var cooldownCount = _audiverisTimeoutFamilies.Count;
        var strikeCount = _audiverisTimeoutStrikes.Count;
        _audiverisTimeoutFamilies.Clear();
        _audiverisTimeoutStrikes.Clear();
        _audiverisStrikeLastUtc.Clear();
        SaveAudiverisTimeoutFamilies();
        SaveAudiverisTimeoutStrikes();
        UpdateAudiverisStatus();
        Log($"🔄 Audiveris: cooldown y strikes reiniciados ({cooldownCount} familias, {strikeCount} strikes).");
    }

    private async void BtnConvertAudiveris_Click(object sender, RoutedEventArgs e)
    {
        Log("🎼 Boton Audiveris pulsado.");
        if (_videoRunning) { Log("⚠️ Hay un vídeo en proceso. Espera a que termine."); return; }

        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder) || !Directory.Exists(destFolder))
        {
            Log("⚠️ Audiveris: carpeta destino no valida.");
            DarkDialogService.ShowMessage(this, "Selecciona una carpeta de destino valida.", "Audiveris", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogDebug($"🎼 Audiveris: carpeta destino {destFolder}");

        var audiverisExe = ResolveAudiverisExecutable();
        if (string.IsNullOrWhiteSpace(audiverisExe))
        {
            Log("⚠️ Audiveris no encontrado. Define AUDIVERIS_EXE o instala Audiveris.");
            DarkDialogService.ShowMessage(this,
                "No se encontro Audiveris. Instala Audiveris o define AUDIVERIS_EXE con la ruta al ejecutable.",
                "Audiveris no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogDebug($"🎼 Audiveris ejecutable: {audiverisExe}");

        var inputs = SafeEnumerateFiles(destFolder, f => AudiverisInputExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        LogDebug($"🎼 Audiveris: candidatos detectados {inputs.Count}");

        if (inputs.Count == 0)
        {
            Log("🎼 Audiveris: no hay partituras PDF/imagen para convertir.");
            return;
        }

        var pendingBeforeCooldown = inputs
            .Where(f => !HasMusicScoreSibling(f))
            .Where(f => !_audiverisKnownPageFailures.Contains(f))
            .ToList();
        var pending = pendingBeforeCooldown
            .Where(f => !IsAudiverisFamilyInTimeoutCooldown(f))
            .ToList();

        var skippedKnownPageFails = inputs.Count(f => !HasMusicScoreSibling(f) && _audiverisKnownPageFailures.Contains(f));
        if (skippedKnownPageFails > 0)
            Log($"ℹ️ Audiveris: {skippedKnownPageFails} archivo(s) omitidos por PAGE fail previo en esta sesión.");
        var skippedCooldownFamilies = pendingBeforeCooldown.Count - pending.Count;
        if (skippedCooldownFamilies > 0)
            Log($"ℹ️ Audiveris: {skippedCooldownFamilies} archivo(s) omitidos por cooldown de timeout activo.");

        if (pending.Count == 0)
        {
            Log("🎼 Audiveris: todo ya estaba convertido (MXL/XML/MSCZ/MSCX).");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates);
        if (skippedFamilyDuplicates > 0)
            Log($"ℹ️ Audiveris: {skippedFamilyDuplicates} archivo(s) omitidos por duplicado de familia (a4/let) en esta corrida.");

        var batch = familyPending.Take(_audiverisBatchSize).ToList();
        var deferred = familyPending.Count - batch.Count;

        var proceed = DarkDialogService.ShowMessage(
            this,
            $"Se convertiran {batch.Count} archivo(s) con Audiveris.\n\nSolo se procesan los que aun no tienen salida MusicScore/MusicXML." +
            (deferred > 0 ? $"\n\nQuedaran {deferred} pendientes para la siguiente corrida." : string.Empty) +
            "\n\n¿Continuar?",
            "Convertir con Audiveris", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (proceed != MessageBoxResult.Yes)
        {
            Log("ℹ️ Audiveris cancelado por usuario.");
            return;
        }

        Log($"🎼 Audiveris: inicio conversion de {batch.Count} archivo(s) (paralelo={_audiverisParallel})." + (deferred > 0 ? $" ({deferred} quedan pendientes)" : string.Empty));

        _audiverisRunning = true;
        btnConvertAudiveris.IsEnabled = false;
        btnSearch.IsEnabled = false;
        btnDownload.IsEnabled = false;
        txtStatus.Text = "🎼 Convirtiendo con Audiveris...";
        var capturedDestFolder = txtDestFolder.Text?.Trim() ?? string.Empty;

        int ok = 0;
        int partial = 0;
        int fail = 0;
        int skippedByFamilyTimeout = 0;
        int processed = 0;
        var timeoutFamilies = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions { MaxDegreeOfParallelism = _audiverisParallel },
                async (input, cancellationToken) =>
                {
                    var name = Path.GetFileName(input);
                    var familyKey = NormalizeAudiverisFamilyKey(input);
                    if (timeoutFamilies.ContainsKey(familyKey))
                    {
                        Interlocked.Increment(ref skippedByFamilyTimeout);
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⏭️ Omitido (familia timeout)" }));
                        return;
                    }

                    var idx = Interlocked.Increment(ref processed);
                    await Dispatcher.InvokeAsync(() =>
                        txtStatus.Text = $"🎼 Audiveris [{idx}/{batch.Count}] {name}");

                    (bool Success, bool Partial, bool PageFailure) result;
                    try
                    {
                        result = await RunAudiverisConversionAsync(audiverisExe, input, capturedDestFolder).ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        Interlocked.Increment(ref fail);
                        timeoutFamilies.TryAdd(familyKey, 0);
                        MarkAudiverisFamilyTimeout(input);
                        Log($"❌ Audiveris timeout en {name}: {tex.Message}");
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⏱️ Timeout" }));
                        return;
                    }

                    if (result.Success)
                    {
                        _audiverisTimeoutStrikes.TryRemove(familyKey, out _);
                        _audiverisStrikeLastUtc.TryRemove(familyKey, out _);
                        if (result.Partial)
                        {
                            Interlocked.Increment(ref partial);
                            Log($"⚠️ Conversión parcial: {name} (solo hoja 1)");
                            await Dispatcher.InvokeAsync(() =>
                                _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⚠️ Parcial (hoja 1)" }));
                        }
                        else
                        {
                            Interlocked.Increment(ref ok);
                            await Dispatcher.InvokeAsync(() =>
                                _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "✅ Convertido" }));
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref fail);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (result.PageFailure)
                            {
                                _audiverisKnownPageFailures.Add(input);
                            }
                            Log($"⚠️ Audiveris sin salida detectada: {name}");
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⚠️ Sin salida" });
                        });
                    }
                }).ConfigureAwait(true);

            if (_audiverisKnownPageFailures.Count > 0)
                SaveAudiverisPageFailures();
            if (!_audiverisTimeoutFamilies.IsEmpty)
                SaveAudiverisTimeoutFamilies();
            if (!_audiverisTimeoutStrikes.IsEmpty)
                SaveAudiverisTimeoutStrikes();
            UpdateAudiverisStatus();
            txtStatus.Text = $"🎼 Conversión finalizada: {ok} completas, {partial} parciales, {fail} sin convertir" +
                             (skippedByFamilyTimeout > 0 ? $", {skippedByFamilyTimeout} omitidos por timeout de familia" : string.Empty);
            Log($"🎼 Conversión Audiveris completada: {ok} completas, {partial} parciales, {fail} sin convertir" +
                (skippedByFamilyTimeout > 0 ? $", {skippedByFamilyTimeout} omitidos por timeout de familia" : string.Empty) +
                (deferred > 0 ? $". Pendientes: {deferred}" : string.Empty));
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error en conversión Audiveris";
            Log($"❌ Audiveris error: {ex.Message}");
            DarkDialogService.ShowMessage(this, $"Error en conversión con Audiveris: {ex.Message}", "Audiveris", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _audiverisRunning = false;
            btnConvertAudiveris.IsEnabled = true;
            btnSearch.IsEnabled = true;
            UpdateDownloadButton();
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string rootDir, Func<string, bool> filter)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(rootDir);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            // resolve real path to detect symlink cycles / junctions
            string realDir;
            try { realDir = Path.GetFullPath(dir); } catch { continue; }
            if (!visited.Add(realDir)) continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); } catch { continue; }
            foreach (var f in files)
                if (filter(f)) yield return f;
            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(dir); } catch { continue; }
            foreach (var d in subdirs) stack.Push(d);
        }
    }

    private static bool HasMusicScoreSibling(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) return false;

        foreach (var ext in AudiverisOutputExtensions)
        {
            var candidate = Path.Combine(dir, stem + ext);
            if (File.Exists(candidate)) return true;
            // Audiveris puede generar stem/stem.ext en subdirectorio
            var subCandidate = Path.Combine(dir, stem, stem + ext);
            if (File.Exists(subCandidate)) return true;
        }
        return false;
    }

    private static string NormalizeAudiverisFamilyKey(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath)?.Trim() ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(inputPath)?.Trim() ?? string.Empty;
        if (stem.EndsWith("-a4", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("_a4", StringComparison.OrdinalIgnoreCase))
            stem = stem[..^3];
        else if (stem.EndsWith("-let", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("_let", StringComparison.OrdinalIgnoreCase))
            stem = stem[..^4];

        string fullDir;
        try { fullDir = Path.GetFullPath(dir); }
        catch { fullDir = dir; }

        return fullDir.ToLowerInvariant() + "|" + stem.ToLowerInvariant();
    }

    private static List<string> SelectFirstPerAudiverisFamily(IEnumerable<string> files, out int skippedDuplicates)
    {
        var bestPerFamily = new Dictionary<string, (string Path, long Size)>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var file in files)
        {
            total++;
            var family = NormalizeAudiverisFamilyKey(file);
            long size;
            try { size = new FileInfo(file).Length; }
            catch { size = long.MaxValue; }

            if (!bestPerFamily.TryGetValue(family, out var current))
            {
                bestPerFamily[family] = (file, size);
                continue;
            }

            if (size < current.Size || (size == current.Size && string.Compare(file, current.Path, StringComparison.OrdinalIgnoreCase) < 0))
                bestPerFamily[family] = (file, size);
        }

        skippedDuplicates = Math.Max(0, total - bestPerFamily.Count);
        return bestPerFamily.Values
            .Select(v => v.Path)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ComputeAudiverisTimeoutSeconds(string inputPath)
    {
        var familyKey = NormalizeAudiverisFamilyKey(inputPath);
        var strike = _audiverisTimeoutStrikes.TryGetValue(familyKey, out var value) ? value : 0;

        var isPdfInput = string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdfInput)
            return _audiverisTimeoutSeconds + (strike * _audiverisTimeoutStrikeBoostSeconds);

        var baseSeconds = Math.Max(_audiverisTimeoutSeconds, _audiverisPdfTimeoutMinSeconds);
        long bytes;
        try { bytes = new FileInfo(inputPath).Length; }
        catch { bytes = 0; }

        var sizeMb = Math.Max(1, (int)Math.Ceiling(bytes / (1024d * 1024d)));
        var adaptive = baseSeconds + (sizeMb * _audiverisPdfTimeoutPerMbSeconds) + (strike * _audiverisTimeoutStrikeBoostSeconds);
        return Math.Clamp(adaptive, _audiverisPdfTimeoutMinSeconds, _audiverisPdfTimeoutMaxSeconds);
    }

    private static bool TryGetAudiverisSiblingVariant(string inputPath, out string siblingPath)
    {
        siblingPath = string.Empty;
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem))
            return false;

        string? siblingStem = null;
        if (stem.EndsWith("-a4", StringComparison.OrdinalIgnoreCase)) siblingStem = stem[..^3] + "-let";
        else if (stem.EndsWith("_a4", StringComparison.OrdinalIgnoreCase)) siblingStem = stem[..^3] + "_let";
        else if (stem.EndsWith("-let", StringComparison.OrdinalIgnoreCase)) siblingStem = stem[..^4] + "-a4";
        else if (stem.EndsWith("_let", StringComparison.OrdinalIgnoreCase)) siblingStem = stem[..^4] + "_a4";

        if (string.IsNullOrWhiteSpace(siblingStem))
            return false;

        var candidate = Path.Combine(dir, siblingStem + ext);
        if (!File.Exists(candidate))
            return false;

        siblingPath = candidate;
        return true;
    }

    private static void DeleteAudiverisPartialOutputs(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) return;

        foreach (var ext in AudiverisOutputExtensions.Concat(new[] { ".omr" }))
        {
            var candidate = Path.Combine(dir, stem + ext);
            try
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
            catch { }
        }
    }

    private static string? ResolveAudiverisExecutable()
    {
        var env = Environment.GetEnvironmentVariable("AUDIVERIS_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        // Also search PATH for audiveris executable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in new[] { "audiveris.bat", "audiveris.cmd", "audiveris.exe", "Audiveris.bat" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return p;
            }
        }

        // Common install locations including .exe variants
        var extendedCandidates = new[]
        {
            @"C:\Program Files\Audiveris\bin\Audiveris.bat",
            @"C:\Program Files\Audiveris\bin\audiveris.bat",
            @"C:\Program Files\Audiveris\bin\Audiveris.exe",
            @"C:\Program Files\Audiveris\bin\audiveris.exe",
            @"C:\Program Files\Audiveris\Audiveris.bat",
            @"C:\Program Files\Audiveris\Audiveris.exe",
            @"C:\Program Files (x86)\Audiveris\bin\Audiveris.bat",
            @"C:\Program Files (x86)\Audiveris\bin\Audiveris.exe",
            @"C:\ProgramData\chocolatey\bin\audiveris.bat",
            @"C:\ProgramData\chocolatey\bin\audiveris.exe",
        };

        foreach (var path in extendedCandidates)
            if (File.Exists(path))
                return path;

        // Scan %USERPROFILE% and common portable locations
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var rel in new[] {
            @"audiveris\bin\audiveris.bat", @"audiveris\bin\audiveris.exe",
            @"Audiveris\bin\Audiveris.bat", @"Audiveris\bin\Audiveris.exe",
            @"Downloads\audiveris\bin\audiveris.bat", @"Downloads\audiveris\bin\audiveris.exe",
            @"Downloads\Audiveris\bin\Audiveris.bat", @"Downloads\Audiveris\bin\Audiveris.exe" })
        {
            var p = Path.Combine(home, rel);
            if (File.Exists(p)) return p;
        }

        return null;
    }

    private async Task<(bool Success, bool Partial, bool PageFailure)> RunAudiverisConversionAsync(string audiverisExe, string inputPath, string fallbackOutputDir = "", bool allowSiblingFallback = true)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? (string.IsNullOrEmpty(fallbackOutputDir) ? string.Empty : fallbackOutputDir);
        static string Tail(string s) => s.Length > 500 ? "..." + s[^500..] : s;

        async Task<(int exitCode, string stdout, string stderr)> RunAudiverisOnceAsync(string currentInputPath, IEnumerable<string> extraArgs)
        {
            var timeoutSeconds = ComputeAudiverisTimeoutSeconds(currentInputPath);
            var audiverisTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = audiverisExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Cap JVM heap. PDF rendering DPI (300) is set via Audiveris user config
            // (run.properties: org.audiveris.omr.image.ImageLoading.pdfResolution=300).
            // Without this, PDFs without embedded DPI get estimated at ~469 DPI → "Error in reaching step PAGE".
            psi.EnvironmentVariables["JAVA_OPTS"] = "-Xmx4g";

            psi.ArgumentList.Add("-batch");
            foreach (var arg in extraArgs)
                psi.ArgumentList.Add(arg);
            psi.ArgumentList.Add("-transcribe");
            psi.ArgumentList.Add("-export");
            psi.ArgumentList.Add("-output");
            psi.ArgumentList.Add(outputDir);
            psi.ArgumentList.Add(currentInputPath);

            var argPreview = string.Join(" ", psi.ArgumentList.Cast<string>());
            LogDebug($"🎼 Audiveris spawn ({Path.GetFileName(currentInputPath)}): timeout={timeoutSeconds}s args={argPreview}");

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(audiverisTimeout)).ConfigureAwait(true);
            if (completed != waitTask)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }

                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(true);
                throw new TimeoutException($"Audiveris timeout tras {audiverisTimeout.TotalMinutes:0.#} min ({timeoutSeconds}s)");
            }

            await waitTask.ConfigureAwait(true);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(true);
            var stdout = (await stdoutTask.ConfigureAwait(true)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(true)).Trim();
            LogDebug($"🎼 Audiveris exit ({Path.GetFileName(currentInputPath)}): code={process.ExitCode}");
            return (process.ExitCode, stdout, stderr);
        }

        (bool Success, bool Partial, bool PageFailure) BuildFailureResult((int exitCode, string stdout, string stderr) attempt, string attemptedPath)
        {
            var firstCombined = string.Join("\n", new[] { attempt.stderr, attempt.stdout }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var isPageFailure = firstCombined.Contains("Error in reaching step PAGE", StringComparison.OrdinalIgnoreCase);
            if (isPageFailure)
            {
                DeleteAudiverisPartialOutputs(attemptedPath);
                Log($"⚠️ Audiveris PAGE fail en {Path.GetFileName(attemptedPath)}. Se omite y se limpia salida parcial.");
                return (false, false, true);
            }

            DeleteAudiverisPartialOutputs(attemptedPath);
            var combined = string.Join(" | ", new[] { attempt.stderr, attempt.stdout }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Tail));
            var msg = string.IsNullOrWhiteSpace(combined) ? $"exit={attempt.exitCode}" : $"exit={attempt.exitCode} {combined}";
            Log($"⚠️ Audiveris fallo ({Path.GetFileName(attemptedPath)}): {msg}");
            return (false, false, false);
        }

        try
        {
            var firstTry = await RunAudiverisOnceAsync(inputPath, Array.Empty<string>()).ConfigureAwait(true);
            if (firstTry.exitCode == 0)
                return (HasMusicScoreSibling(inputPath), false, false);

            var firstFail = BuildFailureResult(firstTry, inputPath);
            if (!allowSiblingFallback || firstFail.PageFailure)
                return firstFail;

            if (!TryGetAudiverisSiblingVariant(inputPath, out var siblingPath))
                return firstFail;
            if (HasMusicScoreSibling(siblingPath) || IsAudiverisFamilyInTimeoutCooldown(siblingPath))
                return firstFail;

            Log($"ℹ️ Audiveris fallback: {Path.GetFileName(inputPath)} fallo; reintento con variante hermana {Path.GetFileName(siblingPath)}.");
            var siblingTry = await RunAudiverisOnceAsync(siblingPath, Array.Empty<string>()).ConfigureAwait(true);
            if (siblingTry.exitCode == 0)
                return (true, false, false);
            return BuildFailureResult(siblingTry, siblingPath);
        }
        catch (TimeoutException) when (allowSiblingFallback && TryGetAudiverisSiblingVariant(inputPath, out var siblingPath) && !HasMusicScoreSibling(siblingPath) && !IsAudiverisFamilyInTimeoutCooldown(siblingPath))
        {
            Log($"ℹ️ Audiveris fallback por timeout: {Path.GetFileName(inputPath)}; reintento con variante hermana {Path.GetFileName(siblingPath)}.");
            var siblingTry = await RunAudiverisOnceAsync(siblingPath, Array.Empty<string>()).ConfigureAwait(true);
            if (siblingTry.exitCode == 0)
                return (true, false, false);
            return BuildFailureResult(siblingTry, siblingPath);
        }
    }

    // ── Video generation (MuseScore directo) ───────────────────────────────────

    private static readonly string[] VideoInputExtensions = [".mscz", ".mxl", ".xml", ".musicxml"];

    private volatile bool _videoRunning;
    private readonly int _videoTimeoutSeconds = ReadFeatureFlagInt("SCOREDOWN_VIDEO_TIMEOUT_SEC", 600, 30, 7200);
    private readonly int _videoParallel = ReadFeatureFlagInt("SCOREDOWN_VIDEO_PARALLEL", 1, 1, 4);

    private async void BtnGenerateVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_audiverisRunning) { Log("⚠️ Hay una conversión Audiveris en proceso. Espera a que termine."); return; }

        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder) || !Directory.Exists(destFolder))
        {
            DarkDialogService.ShowMessage(this, "Selecciona una carpeta de destino válida.", "Generar vídeo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var museScoreExe = await ResolveMuseScoreExecutableAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(museScoreExe))
        {
            DarkDialogService.ShowMessage(this,
                "No se encontró MuseScore. Instala MuseScore o define MUSESCORE_EXE con la ruta al ejecutable.",
                "MuseScore no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var extraVideoArgs = ResolveMuseScoreVideoArgs();
        var soundProfile = GetArgValue(extraVideoArgs, "--sound-profile") ?? "MuseSounds";

        var inputs = SafeEnumerateFiles(destFolder, f => VideoInputExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (inputs.Count == 0)
        {
            Log("🎥 Video: no hay partituras MSCZ/MXL/XML/MusicXML para convertir.");
            return;
        }

        var pending = inputs.Where(f => !HasVideoSibling(f)).ToList();
        if (pending.Count == 0)
        {
            Log("🎥 Video: todo ya tiene vídeo MP4 generado.");
            return;
        }

        Log($"🎥 Video: ejecución automática para {pending.Count} partitura(s). Perfil audio: {soundProfile}");

        _videoRunning = true;
        btnGenerateVideo.IsEnabled = false;
        btnSearch.IsEnabled = false;
        btnDownload.IsEnabled = false;
        txtStatus.Text = "🎥 Generando vídeos...";

        var videoTimeout = TimeSpan.FromSeconds(_videoTimeoutSeconds);
        int ok = 0, fail = 0, processed = 0;
        try
        {
            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions { MaxDegreeOfParallelism = _videoParallel },
                async (input, _) =>
                {
                    var name = Path.GetFileName(input);
                    var idx = Interlocked.Increment(ref processed);
                    var outputMp4 = Path.Combine(
                        Path.GetDirectoryName(input) ?? destFolder,
                        Path.GetFileNameWithoutExtension(input) + ".mp4");

                    await Dispatcher.InvokeAsync(() =>
                        txtStatus.Text = $"🎥 Video [{idx}/{pending.Count}] {name}");
                    LogDebug($"Video [{idx}/{pending.Count}] {name}");

                    bool generated;
                    try
                    {
                        var videoTask = RunMuseScoreVideoAsync(museScoreExe, input, outputMp4, extraVideoArgs);
                        var completed = await Task.WhenAny(videoTask, Task.Delay(videoTimeout)).ConfigureAwait(false);
                        if (completed != videoTask)
                            throw new TimeoutException($"MuseScore timeout tras {videoTimeout.TotalMinutes:0.#} min");
                        generated = await videoTask.ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        Interlocked.Increment(ref fail);
                        Log($"❌ Video timeout en {name}: {tex.Message}");
                        return;
                    }

                    if (generated) Interlocked.Increment(ref ok);
                    else { Interlocked.Increment(ref fail); Log($"⚠️ Sin vídeo generado: {name}"); }
                }).ConfigureAwait(true);

            var msg = $"🎥 Vídeos completados: {ok} OK, {fail} sin generar";
            txtStatus.Text = msg;
            Log(msg);
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error en generación de vídeo";
            Log($"❌ Video error: {ex.Message}");
            DarkDialogService.ShowMessage(this, $"Error generando vídeos: {ex.Message}", "Vídeo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _videoRunning = false;
            btnGenerateVideo.IsEnabled = true;
            btnSearch.IsEnabled = true;
            UpdateDownloadButton();
        }
    }

    private static bool HasVideoSibling(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) return false;
        return File.Exists(Path.Combine(dir, stem + ".mp4"))
            || File.Exists(Path.Combine(dir, stem, stem + ".mp4"));
    }

    private static List<string> ResolveMuseScoreVideoArgs()
    {
        var raw = Environment.GetEnvironmentVariable("MUSESCORE_VIDEO_ARGS");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return [.. parts];
        }

        var soundProfile = Environment.GetEnvironmentVariable("MUSESCORE_SOUND_PROFILE");
        if (string.IsNullOrWhiteSpace(soundProfile))
            soundProfile = "MuseSounds";

        soundProfile = NormalizeSoundProfile(soundProfile);

        // Máxima calidad por defecto
        return ["--resolution", "2160p", "--fps", "60", "--sound-profile", soundProfile];
    }

    private static string NormalizeSoundProfile(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "MuseSounds";

        if (normalized.Equals("vienna", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("vsl", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("vienna symphonic library", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("musesounds", StringComparison.OrdinalIgnoreCase))
            return "MuseSounds";

        return normalized;
    }

    private static string? GetArgValue(IReadOnlyList<string> args, string key)
    {
        for (int i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static async Task<string?> ResolveMuseScoreExecutableAsync()
    {
        var env = Environment.GetEnvironmentVariable("MUSESCORE_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var candidates = new[]
        {
            @"C:\Program Files\MuseScore 4 Testing\bin\MuseScore4.exe",
            @"C:\Program Files\MuseScore Studio Beta\bin\MuseScore4.exe",
            @"C:\Program Files\MuseScore Studio Beta\bin\MuseScore.exe",
            @"C:\Program Files\MuseScore 4\bin\MuseScore4.exe",
            @"C:\Program Files\MuseScore 4\bin\MuseScore.exe",
            @"C:\Program Files\MuseScore 3\bin\MuseScore3.exe",
            @"C:\Program Files\MuseScore 3\bin\MuseScore.exe",
            @"C:\ProgramData\chocolatey\bin\musescore.exe"
        };
        foreach (var path in candidates)
            if (File.Exists(path))
                return path;

        var commandCandidates = new[] { "MuseScore4", "MuseScore3", "MuseScore", "mscore", "mscore3" };
        foreach (var cmd in commandCandidates)
            if (await CanExecuteAsync(cmd, "--version").ConfigureAwait(false))
                return cmd;

        return null;
    }

    private static async Task<bool> CanExecuteAsync(string fileName, string probeArg)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(probeArg);
            using var p = new System.Diagnostics.Process { StartInfo = psi };
            p.Start();
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            var waitTask = p.WaitForExitAsync();
            var allDone = Task.WhenAll(waitTask, stdoutTask, stderrTask);
            var completed = await Task.WhenAny(allDone, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            if (completed != allDone)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RunMuseScoreVideoAsync(string museScoreExe, string inputPath, string outputMp4, IReadOnlyList<string> extraVideoArgs)
    {
        var args = new List<string>(extraVideoArgs.Count + 5);
        args.Add("--score-video");
        args.AddRange(extraVideoArgs);
        args.Add("-o");
        args.Add(outputMp4);
        args.Add(inputPath);
        var autoOk = await RunProcessAsync(museScoreExe, args, Path.GetFileName(inputPath), "MuseScore MP4", TimeSpan.FromSeconds(_videoTimeoutSeconds)).ConfigureAwait(true);
        return autoOk && File.Exists(outputMp4);
    }

    private static bool IsMuseScoreInteractiveForced()
    {
        var raw = Environment.GetEnvironmentVariable("MUSESCORE_INTERACTIVE");
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("si", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> RunMuseScoreInteractiveExportAsync(string museScoreExe, string inputPath, string outputMp4, IReadOnlyList<string> extraVideoArgs)
    {
        var pre = DarkDialogService.ShowMessage(
            this,
            $"Modo interactivo MuseScore beta.\n\n1) Se abrirá: {Path.GetFileName(inputPath)}\n2) Exporta manualmente a MP4 en esta ruta:\n{outputMp4}\n3) Cierra MuseScore para continuar.\n\n¿Abrir ahora?",
            "Exportar vídeo manual", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (pre != MessageBoxResult.Yes)
            return false;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = museScoreExe,
            UseShellExecute = true,
            CreateNoWindow = false
        };
        foreach (var arg in extraVideoArgs)
            psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(inputPath);

        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
        {
            Log($"⚠️ No se pudo abrir MuseScore interactivo ({Path.GetFileName(inputPath)}).");
            return false;
        }

        await process.WaitForExitAsync().ConfigureAwait(true);
        if (File.Exists(outputMp4))
            return true;

        Log($"⚠️ No se encontró MP4 tras cerrar MuseScore ({Path.GetFileName(inputPath)}).");
        return false;
    }

    private async Task<bool> RunProcessAsync(string exe, IEnumerable<string> args, string inputName, string stage, TimeSpan? timeout = null)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync();

        // Leer pipes en paralelo con WaitForExit para evitar deadlock por buffer lleno
        var allDone = Task.WhenAll(waitTask, stdoutTask, stderrTask);
        if (timeout.HasValue)
        {
            var completed = await Task.WhenAny(allDone, Task.Delay(timeout.Value)).ConfigureAwait(true);
            if (completed != allDone)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                await allDone.ConfigureAwait(true);
                Log($"⚠️ {stage} timeout ({inputName}): proces killed tras {timeout.Value.TotalSeconds:0}s");
                return false;
            }
        }
        else
        {
            await allDone.ConfigureAwait(true);
        }

        if (process.ExitCode == 0)
            return true;

        var err = (await stderrTask.ConfigureAwait(true)).Trim();
        var msg = string.IsNullOrWhiteSpace(err) ? $"exit={process.ExitCode}" : err;
        Log($"⚠️ {stage} fallo ({inputName}): {msg}");
        return false;
    }

    private void PrepareQueue(IEnumerable<(PartituraItem item, PartituraFile file)> jobs)
    {
        _downloadQueue.Clear();
        foreach (var job in jobs)
        {
            _downloadQueue.Add(new DownloadQueueItem
            {
                FileName = job.file.FileName,
                SourceItem = job.item,
                SourceFile = job.file,
                Status = _pausedQueueFiles.ContainsKey(job.file.DownloadUrl) ? $"Pausado · {job.item.DisplayName}" : $"Pendiente · {job.item.DisplayName}",
                Percent = 0
            });
        }
        lstQueue.Items.Refresh();
    }

    private void UpdateQueue(PartituraFile file, string state, double percent, double speedKBps)
    {
        if (_downloadQueue is null) return;

        var item = _downloadQueue.FirstOrDefault(q =>
            string.Equals(q.FileName, file.FileName, StringComparison.OrdinalIgnoreCase) && q.Percent < 100.0);
        if (item is null)
            item = _downloadQueue.FirstOrDefault(q => string.Equals(q.FileName, file.FileName, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;

        item.Percent = percent;
        item.Status = state switch
        {
            "starting" => "Iniciando",
            "downloading" => $"Descargando · {speedKBps:F0} KB/s",
            "success" => "Completado",
            "skipped" => "Ya existía",
            "paused" => "Pausado",
            "cancelled" => "Cancelado",
            "error" => "Error",
            _ => state
        };

        if (!item.HistoryRecorded && item.SourceItem is not null && item.SourceFile is not null && state is "success" or "skipped")
        {
            item.HistoryRecorded = true;
            var composerFolder = DownloadService.SanitizeFolderNameStatic(
                string.IsNullOrEmpty(item.SourceItem.Composer) ? "Varios" : item.SourceItem.Composer);
            var expectedPath = string.IsNullOrEmpty(_currentDestFolder)
                ? null
                : Path.Combine(_currentDestFolder, composerFolder, item.SourceFile.FileName);
            RecordDownload(item.SourceItem, item.SourceFile, new DownloadResult
            {
                Success = state == "success",
                Skipped = state == "skipped",
                BytesDownloaded = item.SourceFile.SizeBytes,
                FilePath = expectedPath
            }, item.SourceItem.Source);
        }

        Dispatcher.BeginInvoke(() => lstQueue?.Items.Refresh(), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RecordDownload(PartituraItem item, PartituraFile file, DownloadResult result, string source)
    {
        var entry = new DownloadHistoryItem
        {
            FileName = file.FileName,
            Source = source,
            DownloadedAt = DateTime.Now,
            SizeBytes = result.BytesDownloaded > 0 ? result.BytesDownloaded : file.SizeBytes,
            Status = result.Skipped ? "Ya existía" : "OK",
            FilePath = result.FilePath
        };
        _downloadHistory.Insert(0, entry);
        while (_downloadHistory.Count > 30)
            _downloadHistory.RemoveAt(_downloadHistory.Count - 1);
        SaveDownloadHistory();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ClearResults()
    {
        _allResults.Clear();
        _filtered.Clear();
        _knownComposersCache.Clear();
        lstResults.ItemsSource = null;
        lstResults.ItemsSource = _filtered;
        txtResultCount.Text = "Sin resultados";
        btnDownload.IsEnabled = false;
        btnExportJson.IsEnabled = false;
        btnExportCsv.IsEnabled = false;
        txtInfoHint.Visibility = Visibility.Visible;
        pnlInfoContent.Visibility = Visibility.Collapsed;
    }


    private void Log(string msg)
    {
        // Write to file
        _fileLogger?.Log(msg);

        // Write to UI
        Dispatcher.InvokeAsync(() =>
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            txtLog.ScrollToEnd();
        });
    }

    private void LogDebug(string msg)
    {
        // File only, no UI noise
        _fileLogger?.Log($"[DEBUG] {msg}");
    }

    private static string NormalizeKey(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string BuildDedupKey(PartituraItem item)
    {
        var head = NormalizeKey(item.Composer) + "|" + NormalizeKey(item.Title);
        var signature = string.Join(";", item.Files
            .Select(f => NormalizeKey(f.Format) + ":" + NormalizeKey(Path.GetFileNameWithoutExtension(f.FileName)) + ":" + NormalizeUrl(f.DownloadUrl))
            .OrderBy(v => v));
        return head + "|" + signature;
    }

    private static string BuildTagKey(PartituraItem item)
        => NormalizeKey(item.Source) + "|" + BuildDedupKey(item);

    private void UpdateCacheStats()
    {
        PurgeExpiredCache();
        txtCacheStats.Text = $"Cache H:{_cacheHits} M:{_cacheMisses} ({_searchCache.Count})";
    }

    private void UpdateSessionStats()
    {
        txtSessionStats.Text = $"S:{_sessionSearches} · Obras:{_sessionResults} · Desc:{_sessionDownloads}";
    }

    private void ApplySavedTag(PartituraItem item)
    {
        var key = BuildTagKey(item);
        if (_savedTags.TryGetValue(key, out var tag))
        {
            item.UserTag = tag;
            return;
        }

        // Compatibilidad hacia atrás: tags guardados antes de incluir Source en la clave.
        var legacyKey = BuildDedupKey(item);
        item.UserTag = _savedTags.TryGetValue(legacyKey, out var legacyTag) ? legacyTag : string.Empty;
    }

    private void LoadDownloadHistory()
    {
        var items = JsonStore.Load<List<DownloadHistoryItem>>(_downloadHistoryPath, []);
        _downloadHistory.Clear();
        foreach (var item in items.Take(30)) _downloadHistory.Add(item);
    }

    private void SaveDownloadHistory()
        => JsonStore.Save(_downloadHistoryPath, _downloadHistory.ToList());

    private void LoadOfflineLibrary()
    {
        _offlineLibraryItems = JsonStore.Load<List<PartituraItem>>(_offlineLibraryPath, []);
    }

    private void SaveOfflineLibrary()
        => JsonStore.Save(_offlineLibraryPath, _offlineLibraryItems);

    private void LoadTags()
    {
        var tags = JsonStore.Load<Dictionary<string, string>>(_tagsPath, []);
        _savedTags.Clear();
        foreach (var kv in tags) _savedTags[kv.Key] = kv.Value;
    }

    private void SaveTags()
        => JsonStore.Save(_tagsPath, _savedTags);

    private void LoadAudiverisPageFailures()
    {
        var entries = JsonStore.Load<List<string>>(_audiverisPageFailuresPath, []);
        _audiverisKnownPageFailures.Clear();
        foreach (var path in entries)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            try
            {
                _audiverisKnownPageFailures.Add(Path.GetFullPath(path));
            }
            catch { }
        }

        if (_audiverisKnownPageFailures.Count > 0)
            Log($"ℹ️ Audiveris: {_audiverisKnownPageFailures.Count} PAGE fail(s) cargados de sesión previa.");
    }

    private void SaveAudiverisPageFailures()
        => JsonStore.Save(_audiverisPageFailuresPath, _audiverisKnownPageFailures.OrderBy(p => p).ToList());

    private sealed class AudiverisTimeoutFamilyEntry
    {
        public string FamilyKey { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }

    private void LoadAudiverisTimeoutFamilies()
    {
        var now = DateTime.UtcNow;
        var entries = JsonStore.Load<List<AudiverisTimeoutFamilyEntry>>(_audiverisTimeoutFamiliesPath, []);
        _audiverisTimeoutFamilies.Clear();
        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.FamilyKey) || entry.ExpiresUtc <= now)
                continue;

            _audiverisTimeoutFamilies[entry.FamilyKey] = entry.ExpiresUtc;
        }

        if (_audiverisTimeoutFamilies.Count > 0)
            Log($"ℹ️ Audiveris: {_audiverisTimeoutFamilies.Count} familia(s) en cooldown por timeout cargadas de sesión previa.");
    }

    private void SaveAudiverisTimeoutFamilies()
    {
        var now = DateTime.UtcNow;
        var snapshot = _audiverisTimeoutFamilies
            .Where(kv => kv.Value > now)
            .Select(kv => new AudiverisTimeoutFamilyEntry { FamilyKey = kv.Key, ExpiresUtc = kv.Value })
            .OrderBy(x => x.FamilyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        JsonStore.Save(_audiverisTimeoutFamiliesPath, snapshot);
    }

    private void UpdateAudiverisStatus()
    {
        if (!IsInitialized || txtAudiverisStatus is null) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateAudiverisStatus, DispatcherPriority.Background);
            return;
        }
        var now = DateTime.UtcNow;
        var activeFamilies = _audiverisTimeoutFamilies
            .Where(kv => kv.Value > now)
            .OrderBy(kv => kv.Value)
            .ToList();
        var strikeCount = _audiverisTimeoutStrikes.Count(kv => kv.Value > 0);
        var cooldownCount = activeFamilies.Count;
        txtAudiverisStatus.Text = cooldownCount > 0 || strikeCount > 0
            ? $"Cooldown: {cooldownCount} | Strikes: {strikeCount}"
            : string.Empty;
        if (cooldownCount > 0)
        {
            var lines = activeFamilies.Take(10).Select(kv =>
            {
                var key = kv.Key.Length > 60 ? kv.Key[^60..] : kv.Key;
                var remaining = (kv.Value - now).TotalMinutes;
                var strikes = _audiverisTimeoutStrikes.TryGetValue(kv.Key, out var s) ? s : 0;
                return $"{key}  [{remaining:F0}min, s{strikes}]";
            });
            txtAudiverisStatus.ToolTip = "Familias en cooldown:\n" + string.Join("\n", lines) +
                (cooldownCount > 10 ? $"\n... +{cooldownCount - 10} más" : string.Empty);
        }
        else
        {
            txtAudiverisStatus.ToolTip = null;
        }
    }

    private sealed class AudiverisStrikeRecord
    {
        public int Strikes { get; set; }
        public DateTime LastTimeoutUtc { get; set; }
    }

    private void LoadAudiverisTimeoutStrikes()
    {
        var decayHours = _audiverisStrikeDecayHours;
        var now = DateTime.UtcNow;
        var data = JsonStore.Load<Dictionary<string, AudiverisStrikeRecord>>(_audiverisTimeoutStrikesPath, []);
        _audiverisTimeoutStrikes.Clear();
        _audiverisStrikeLastUtc.Clear();
        foreach (var kv in data)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null || kv.Value.Strikes <= 0) continue;
            // Decay: -1 strike per decayHours elapsed since last timeout
            var hoursElapsed = (now - kv.Value.LastTimeoutUtc).TotalHours;
            var decayed = (int)Math.Floor(hoursElapsed / decayHours);
            var strikes = Math.Max(0, kv.Value.Strikes - decayed);
            if (strikes <= 0) continue;
            _audiverisTimeoutStrikes[kv.Key] = Math.Clamp(strikes, 1, 12);
            _audiverisStrikeLastUtc[kv.Key] = kv.Value.LastTimeoutUtc;
        }
    }

    private void SaveAudiverisTimeoutStrikes()
    {
        var snapshot = _audiverisTimeoutStrikes
            .Where(kv => kv.Value > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => new AudiverisStrikeRecord
                {
                    Strikes = kv.Value,
                    LastTimeoutUtc = _audiverisStrikeLastUtc.TryGetValue(kv.Key, out var ts) ? ts : DateTime.UtcNow
                },
                StringComparer.OrdinalIgnoreCase);
        JsonStore.Save(_audiverisTimeoutStrikesPath, snapshot);
    }

    private bool IsAudiverisFamilyInTimeoutCooldown(string inputPath)
    {
        var familyKey = NormalizeAudiverisFamilyKey(inputPath);
        if (!_audiverisTimeoutFamilies.TryGetValue(familyKey, out var expiresUtc))
            return false;

        if (expiresUtc > DateTime.UtcNow)
            return true;

        _audiverisTimeoutFamilies.TryRemove(familyKey, out _);
        return false;
    }

    private void MarkAudiverisFamilyTimeout(string inputPath)
    {
        var familyKey = NormalizeAudiverisFamilyKey(inputPath);
        var now = DateTime.UtcNow;

        // Increment strikes first (cap at 12)
        var newStrikes = _audiverisTimeoutStrikes.AddOrUpdate(
            familyKey,
            1,
            (_, current) => Math.Clamp(current + 1, 1, 12));
        _audiverisStrikeLastUtc[familyKey] = now;

        // Exponential backoff: cooldownMinutes × 2^(strikes-1), capped at 7 days (10080 min)
        var multiplier = Math.Min(Math.Pow(2, newStrikes - 1), 1024.0);
        var cooldownMinutes = (long)Math.Min(_audiverisTimeoutCooldownMinutes * multiplier, 10080.0);
        var expiresUtc = now.AddMinutes(cooldownMinutes);
        _audiverisTimeoutFamilies.AddOrUpdate(
            familyKey,
            expiresUtc,
            (_, current) => current > expiresUtc ? current : expiresUtc);
    }

    private void BtnExportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = DarkDialogService.PromptSaveFile(
            this,
            "Exportar biblioteca local",
            Path.Combine(txtDestFolder.Text?.Trim() ?? string.Empty, $"biblioteca_partituras_{DateTime.Now:yyyyMMdd_HHmm}.json"),
            ".json");
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        var payload = new LibraryData
        {
            Tags = new Dictionary<string, string>(_savedTags),
            Items = CloneItems(_allResults.Any() ? _allResults : _offlineLibraryItems)
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, ScoreDownJsonContext.Default.LibraryData));
        Log($"📚 Biblioteca exportada: {outputPath}");
    }

    private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = DarkDialogService.PromptSaveFile(
            this,
            "Exportar índice HTML",
            Path.Combine(txtDestFolder.Text?.Trim() ?? string.Empty, $"biblioteca_partituras_{DateTime.Now:yyyyMMdd_HHmm}.html"),
            ".html");
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        try
        {
            var items = _allResults.Any() ? _allResults.ToList() : _offlineLibraryItems;
            var destFolder = txtDestFolder.Text?.Trim() ?? string.Empty;
            var html = LibraryHtmlExporter.GenerateHtml(items, destFolder);
            File.WriteAllText(outputPath, html, System.Text.Encoding.UTF8);
            Log($"🌐 Índice HTML exportado: {outputPath}");

            // Abrir en navegador por defecto
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
        catch (Exception ex)
        {
            DarkDialogService.ShowMessage(this, $"Error exportando HTML: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportLibrary_Click(object sender, RoutedEventArgs e)
    {
        var inputPath = DarkDialogService.PromptOpenFile(
            this,
            "Importar biblioteca local",
            Path.Combine(txtDestFolder.Text?.Trim() ?? string.Empty, "biblioteca_partituras.json"),
            ".json");
        if (string.IsNullOrWhiteSpace(inputPath)) return;

        try
        {
            var data = JsonSerializer.Deserialize(File.ReadAllText(inputPath), ScoreDownJsonContext.Default.LibraryData);
            if (data is null) return;
            _savedTags.Clear();
            foreach (var kv in data.Tags)
                _savedTags[kv.Key] = kv.Value;
            _offlineLibraryItems = CloneItems(data.Items);

            SaveTags();
            SaveOfflineLibrary();
            foreach (var item in _allResults)
                ApplySavedTag(item);
            lstResults.Items.Refresh();
            ApplyFilter();
            if (lstResults.SelectedItem is PartituraItem selected)
                UpdateSelectedItemPanel(selected);
            Log($"📥 Biblioteca importada: {inputPath}");
        }
        catch (Exception ex)
        {
            DarkDialogService.ShowMessage(this, $"No se pudo importar biblioteca: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        if (bytes >= 1024L * 1024L) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }

    private void PurgeExpiredCache()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _searchCache.Where(kv => kv.Value.Expires <= now).Select(kv => kv.Key))
            _searchCache.TryRemove(key, out _);
    }

    private void RefreshHistoryCombo()
    {
        cmbHistory.ItemsSource = null;
        cmbHistory.ItemsSource = _recentQueries;
    }

    private void AddToSearchHistory(string query)
    {
        var normalized = query.Trim();
        if (string.IsNullOrEmpty(normalized)) return;

        _recentQueries.RemoveAll(q => string.Equals(q, normalized, StringComparison.OrdinalIgnoreCase));
        _recentQueries.Insert(0, normalized);
        if (_recentQueries.Count > MaxRecentQueries)
            _recentQueries.RemoveRange(MaxRecentQueries, _recentQueries.Count - MaxRecentQueries);

        RefreshHistoryCombo();
        SaveSearchHistory();
    }

    private void LoadSearchHistory()
    {
        var entries = JsonStore.Load<List<string>>(_historyPath, []);
        _recentQueries.Clear();
        foreach (var item in entries.Where(x => !string.IsNullOrWhiteSpace(x)).Take(MaxRecentQueries))
            _recentQueries.Add(item.Trim());
    }

    private void SaveSearchHistory()
        => JsonStore.Save(_historyPath, _recentQueries);

    private void LoadUiState()
    {
        var state = JsonStore.Load<UiState>(_uiStatePath, new UiState());
        if (!string.IsNullOrWhiteSpace(state.DestinationFolder))
            txtDestFolder.Text = state.DestinationFolder;
        SelectComboItemByText(cmbSource, state.Source);
        SelectComboItemByText(cmbFilterSource, state.FilterSource);
        if (state.EnableMutopia.HasValue)
        {
            _enableMutopia = state.EnableMutopia.Value;
            chkEnableMutopia.IsChecked = _enableMutopia;
        }
        if (state.EnableCpdl.HasValue)
        {
            _enableCpdl = state.EnableCpdl.Value;
            chkEnableCpdl.IsChecked = _enableCpdl;
        }
        if (state.EnableMusopen.HasValue)
        {
            _enableMusopen = state.EnableMusopen.Value;
            chkEnableMusopen.IsChecked = _enableMusopen;
        }
        // Restaurar sesión Musopen persistida
        if (!string.IsNullOrWhiteSpace(state.MusopenCookieHeader))
        {
            _musopen.SetSession(state.MusopenCookieHeader, state.MusopenUserAgent);
            Log("🔐 Musopen sesión restaurada desde estado guardado.");
        }
        btnCpdlSession.IsEnabled = _enableCpdl;
        btnMusopenSession.IsEnabled = _enableMusopen;
    }

    private void SaveUiState()
    {
        var (muCookie, muUA) = _musopen.GetSessionHeaders();
        JsonStore.Save(_uiStatePath, new UiState
        {
            DestinationFolder = txtDestFolder.Text,
            Source = (cmbSource.SelectedItem as ComboBoxItem)?.Content?.ToString(),
            FilterSource = (cmbFilterSource.SelectedItem as ComboBoxItem)?.Content?.ToString(),
            EnableMutopia = _enableMutopia,
            EnableCpdl = _enableCpdl,
            EnableMusopen = _enableMusopen,
            MusopenCookieHeader = muCookie,
            MusopenUserAgent = muUA
        });
    }

    private static void SelectComboItemByText(System.Windows.Controls.ComboBox combo, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (string.Equals(text, "Ambas", StringComparison.OrdinalIgnoreCase))
            text = "Todas";
        if (string.Equals(text, "IMSLP", StringComparison.OrdinalIgnoreCase))
            text = "Mutopia";
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi &&
                string.Equals(cbi.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
    }

    private async Task<List<PartituraItem>> FetchWithCacheAsync(
        string source,
        string query,
        Func<IProgress<string>?, CancellationToken, Task<List<PartituraItem>>> fetch,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // R7: ConcurrentDictionary — safe from concurrent Task.WhenAll calls; purge must be non-mutating here.
        var key = NormalizeKey(source) + "|" + NormalizeKey(query);
        var now = DateTimeOffset.UtcNow;

        if (_searchCache.TryGetValue(key, out var entry) && entry.Expires > now)
        {
            _cacheHits++;
            _searchCache.TryUpdate(key, (entry.Expires, now, entry.Results), entry);
            UpdateCacheStats();
            progress?.Report($"⚡ Cache {source}: {entry.Results.Count} obras");
            return CloneItems(entry.Results);
        }

        _cacheMisses++;
        UpdateCacheStats();
        var fresh = await fetch(progress, ct);
        var newEntry = (Expires: now.Add(SearchCacheTtl), LastAccessed: now, Results: CloneItems(fresh));
        _searchCache[key] = newEntry;

        // LRU eviction: keep only MaxCacheEntries newest-accessed (best-effort; slight race is acceptable)
        if (_searchCache.Count > MaxCacheEntries)
        {
            var oldest = _searchCache.OrderBy(kv => kv.Value.LastAccessed).First().Key;
            _searchCache.TryRemove(oldest, out _);
        }

        return CloneItems(fresh);
    }

    private static List<PartituraItem> CloneItems(IEnumerable<PartituraItem> items)
    {
        return items.Select(i => new PartituraItem
        {
            Title = i.Title,
            Composer = i.Composer,
            PageUrl = i.PageUrl,
            IsSelected = i.IsSelected,
            Source = i.Source,
            SourcePageId = i.SourcePageId,
            UserTag = i.UserTag,
            Genre = i.Genre,
            Instrument = i.Instrument,
            License = i.License,
            Files = i.Files.Select(f => new PartituraFile
            {
                Format = f.Format,
                DownloadUrl = f.DownloadUrl,
                FileName = f.FileName,
                SizeBytes = f.SizeBytes
            }).ToList()
        }).ToList();
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var q = url.IndexOf('?');
        return (q >= 0 ? url[..q] : url).Trim().ToUpperInvariant();
    }

    private static int ComputeRelevance(PartituraItem item, string query)
    {
        int score = 0;
        var title = NormalizeKey(item.Title);
        var composer = NormalizeKey(item.Composer);
        var q = NormalizeKey(query);

        if (!string.IsNullOrEmpty(q))
        {
            if (title.Contains(q)) score += 120;
            if (composer.Contains(q)) score += 80;
        }

        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = NormalizeKey(token);
            if (t.Length < 2) continue;
            if (title.Contains(t)) score += 18;
            else if (composer.Contains(t)) score += 12;
        }

        if (item.Files.Any(f => f.Format == "PDF")) score += 6;
        if (item.Files.Any(f => f.Format == "MIDI")) score += 4;
        if (item.Files.Any(f => f.Format is "XML" or "MXL")) score += 2;

        var mb = (int)(item.Files.Sum(f => f.SizeBytes) / (1024 * 1024));
        score += Math.Min(mb, 20);
        return score;
    }

    private sealed class DownloadQueueItem
    {
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Percent { get; set; }
        public PartituraItem? SourceItem { get; set; }
        public PartituraFile? SourceFile { get; set; }
        public bool HistoryRecorded { get; set; }
    }

    private sealed class AudiverisLogItem
    {
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // ── History context-menu handlers ──────────────────────────────────────

    private void CtxHistoryOpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (lstDownloadHistory.SelectedItem is not DownloadHistoryItem h) return;
        var path = h.FilePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        else
            DarkDialogService.ShowMessage(this, "El archivo no se encontró en disco.", "No disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void CtxHistoryOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lstDownloadHistory.SelectedItem is not DownloadHistoryItem h) return;
        var folder = string.IsNullOrEmpty(h.FilePath) ? null : Path.GetDirectoryName(h.FilePath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
        else
            DarkDialogService.ShowMessage(this, "La carpeta no se encontró en disco.", "No disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void LstDownloadHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => CtxHistoryOpenFolder_Click(sender, new RoutedEventArgs());

    // ── ETA helper ────────────────────────────────────────────────────────

    private static string FormatEta(TimeSpan elapsed, double pct)
    {
        if (pct <= 0) return "–";
        var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds / pct * (100.0 - pct));
        return remaining.TotalMinutes >= 1
            ? $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s"
            : $"{(int)remaining.TotalSeconds}s";
    }

    private static bool ReadFeatureFlag(string envKey, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        raw = raw.Trim();
        if (raw is "1" or "true" or "yes" or "on") return true;
        if (raw is "0" or "false" or "no" or "off") return false;
        return defaultValue;
    }

    private static int ReadFeatureFlagInt(string envKey, int defaultValue, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw.Trim(), out var parsed)) return defaultValue;
        if (parsed < min) return min;
        if (parsed > max) return max;
        return parsed;
    }

}
