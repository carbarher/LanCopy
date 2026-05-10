using ScoreDown.Infrastructure;
using ScoreDown.Models;
using ScoreDown.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
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
    private readonly MusopenService _musopen = new();
    private readonly OpenScoreService _openScore = new();
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
    private readonly string _audiverisTelemetryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "audiveris-timeout-telemetry.json");
    private readonly string _oemerTelemetryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "oemer-timeout-telemetry.json");
    private bool _enableMutopia = ReadFeatureFlag("SCOREDOWN_ENABLE_MUTOPIA", true);
    private bool _enableMusopen = ReadFeatureFlag("SCOREDOWN_ENABLE_MUSOPEN", true);
    private bool _enableOpenScore = ReadFeatureFlag("SCOREDOWN_ENABLE_OPENSCORE", true);
    private bool _onlyClassical = ReadFeatureFlag("SCOREDOWN_ONLY_CLASSICAL", true);
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
    // CPDL removido.
    // Dedup set para catálogo — O(1) lookup vs O(N²) .Any()
    // NOTE: was a field (_catalogSeenKeys) — moved to local variable inside DoFetchCatalogAsync
    // to eliminate Clear()-vs-Add() race when a previous run is cancelled mid-flight.
    // Cache de compositores conocidos en resultados actuales — evita iterar _allResults por tecla
    private readonly HashSet<string> _knownComposersCache = new(StringComparer.OrdinalIgnoreCase);
    // Cache de lista merged para sugerencias de compositor — invalida cuando cambia _knownComposersCache
    private List<string>? _composerSuggestionsPool;
    private int _composerPoolVersion;  // inc al añadir a _knownComposersCache
    private static readonly HashSet<string> AudiverisInputExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp" };
    private static readonly HashSet<string> AudiverisOutputExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mxl", ".xml", ".musicxml", ".mscz", ".mscx" };
    private readonly int _audiverisBatchSize = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_BATCH", 100, 1, 10000);
    private readonly int _audiverisTimeoutSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_SEC", 300, 30, 7200);
    private readonly int _audiverisParallel = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_PARALLEL", 3, 1, 8);
    private readonly int _audiverisPdfTimeoutMinSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_MIN_SEC", 420, 60, 7200);
    private readonly int _audiverisPdfTimeoutPerMbSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_PER_MB_SEC", 12, 0, 300);
    private readonly int _audiverisPdfTimeoutPerPageSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_PER_PAGE_SEC", 10, 0, 300);
    private readonly int _audiverisPdfTimeoutMaxSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_PDF_MAX_SEC", 1800, 120, 10800);
    private readonly int _audiverisTimeoutStrikeBoostSeconds = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_STRIKE_BOOST_SEC", 60, 0, 600);
    private readonly int _audiverisTimeoutCooldownMinutes = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_TIMEOUT_COOLDOWN_MIN", 720, 10, 10080);
    private readonly int _audiverisStrikeDecayHours = ReadFeatureFlagInt("SCOREDOWN_AUDIVERIS_STRIKE_DECAY_HOURS", 48, 1, 8760);
    private readonly ConcurrentDictionary<string, byte> _audiverisKnownPageFailures = new(StringComparer.OrdinalIgnoreCase);
    private bool _audiverisKnownPageFailuresDirty;
    private readonly ConcurrentDictionary<string, DateTime> _audiverisTimeoutFamilies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _audiverisTimeoutStrikes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _audiverisStrikeLastUtc = new(StringComparer.OrdinalIgnoreCase);
    private bool _audiverisTimeoutFamiliesDirty;
    private bool _audiverisTimeoutStrikesDirty;
    private volatile bool _audiverisRunning;
    private CancellationTokenSource? _audiverisCts;

    // ── oemer ──────────────────────────────────────────────────────────────
    private readonly ObservableCollection<AudiverisLogItem> _oemerLog = [];
    private readonly string _oemerPageFailuresPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "oemer-page-failures.json");
    private readonly string _oemerTimeoutFamiliesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "oemer-timeout-families.json");
    private readonly string _oemerTimeoutStrikesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "oemer-timeout-strikes.json");
    private readonly int _oemerBatchSize = ReadFeatureFlagInt("SCOREDOWN_OEMER_BATCH", 100, 1, 10000);
    private readonly int _oemerTimeoutSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_SEC", 180, 30, 7200);
    private readonly int _oemerParallel = ReadFeatureFlagInt("SCOREDOWN_OEMER_PARALLEL", 4, 1, 8);
    private readonly int _oemerPdfTimeoutMinSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_PDF_MIN_SEC", 600, 60, 86400);
    private readonly int _oemerPdfTimeoutPerMbSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_PDF_PER_MB_SEC", 30, 0, 600);
    private readonly int _oemerPdfTimeoutPerPageSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_PDF_PER_PAGE_SEC", 15, 0, 300);
    private readonly int _oemerPdfTimeoutMaxSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_PDF_MAX_SEC", 3600, 120, 86400);
    private readonly int _oemerTimeoutStrikeBoostSeconds = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_STRIKE_BOOST_SEC", 120, 0, 1200);
    private readonly int _oemerTimeoutCooldownMinutes = ReadFeatureFlagInt("SCOREDOWN_OEMER_TIMEOUT_COOLDOWN_MIN", 720, 10, 10080);
    private readonly int _oemerStrikeDecayHours = ReadFeatureFlagInt("SCOREDOWN_OEMER_STRIKE_DECAY_HOURS", 48, 1, 8760);
    private readonly ConcurrentDictionary<string, byte> _oemerKnownPageFailures = new(StringComparer.OrdinalIgnoreCase);
    private bool _oemerKnownPageFailuresDirty;
    private readonly ConcurrentDictionary<string, DateTime> _oemerTimeoutFamilies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _oemerTimeoutStrikes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _oemerStrikeLastUtc = new(StringComparer.OrdinalIgnoreCase);
    private bool _oemerTimeoutFamiliesDirty;
    private bool _oemerTimeoutStrikesDirty;
    private volatile bool _oemerRunning;
    private CancellationTokenSource? _oemerCts;

    private PartituraItem? _contextItem;  // item bajo clic derecho
    private readonly ConcurrentDictionary<string, byte> _pausedQueueFiles = new(StringComparer.OrdinalIgnoreCase);

    // Production features: file logging + circuit breaker
    private FileLoggingService? _fileLogger;
    private GlobalCircuitBreaker? _circuitBreaker;
    private int _cleanupDone;
    private int _shutdownRequested;
    // ── Dir-scan cache (reutiliza escaneo raw entre SafeEnumerateFiles y BuildMusicScoreSiblingSet) ──
    private readonly Dictionary<string, (DateTime Time, List<string> Files)> _rawFilesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (DateTime Time, HashSet<string> Set)> _siblingSetCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan _rawFilesCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly System.Text.RegularExpressions.Regex _pageSuffixRegex =
        new(@"^(.+)_p(\d{1,6})$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly object _pdfRendererCacheLock = new();
    private static bool _pdfRendererCacheInitialized;
    private static (string Kind, string Exe)? _pdfRendererCache;
    private static readonly object _oemerCommandCacheLock = new();
    private static bool _oemerCommandCacheInitialized;
    private static (string Exe, string[] PrefixArgs)? _oemerCommandCache;
    private static readonly object _audiverisExeCacheLock = new();
    private static bool _audiverisExeCacheInitialized;
    private static string? _audiverisExeCache;
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

        // _cpdl.RequestInteractiveSessionAsync = OpenCpdlSessionAsync;  // CPDL removido

        chkEnableMutopia.IsChecked = _enableMutopia;
        chkEnableMusopen.IsChecked = _enableMusopen;
        chkEnableOpenScore.IsChecked = _enableOpenScore;
        chkOnlyClassical.IsChecked = _onlyClassical;

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
        LoadOemerPageFailures();
        LoadOemerTimeoutFamilies();
        LoadOemerTimeoutStrikes();
        UpdateOemerStatus();
        lstOemerLog.ItemsSource = _oemerLog;
        UpdateSessionStats();
        UpdateSourceDashboard();
    }

    private void Window_PreviewKeyDown(object sender, WinInput.KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != WinInput.ModifierKeys.Control) return;
        switch (e.Key)
        {
            case WinInput.Key.F:
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible)
                {
                    txtVideoFilter.Focus();
                    txtVideoFilter.SelectAll();
                }
                else
                {
                    txtSearch.Focus();
                    txtSearch.SelectAll();
                }
                e.Handled = true;
                break;
            case WinInput.Key.R:
                // Ctrl+R → toggle Re-generar cuando el panel de vídeo está abierto
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible && !_videoRunning)
                {
                    chkVideoRegenerate.IsChecked = !(chkVideoRegenerate.IsChecked == true);
                    e.Handled = true;
                }
                break;
            case WinInput.Key.G:
                // Ctrl+G → disparar generación cuando el panel está abierto y hay selección
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible
                    && btnVideoSelectGenerate.IsEnabled)
                {
                    BtnVideoSelectGenerate_Click(btnVideoSelectGenerate, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;
            case WinInput.Key.P:
                // Ctrl+P → seleccionar pendientes cuando el panel de vídeo está abierto
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible && !_videoRunning)
                {
                    BtnVideoSelectPending_Click(btnVideoSelectPending, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;
            case WinInput.Key.I:
                // Ctrl+I → invertir selección cuando el panel de vídeo está abierto
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible && !_videoRunning)
                {
                    BtnVideoSelectInvert_Click(btnVideoSelectInvert, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;
            case WinInput.Key.E:
                // Ctrl+E → seleccionar errores cuando el panel de vídeo está abierto
                if (pnlVideoSelect.Visibility == System.Windows.Visibility.Visible
                    && btnVideoSelectErrors.IsEnabled && !_videoRunning)
                {
                    BtnVideoSelectErrors_Click(btnVideoSelectErrors, new RoutedEventArgs());
                    e.Handled = true;
                }
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
        SaveOemerPageFailures();
        SaveOemerTimeoutFamilies();
        SaveOemerTimeoutStrikes();

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

        // try { _cpdlWebSession?.Dispose(); } catch { }  // CPDL removido
        // _cpdlWebSession = null;
        // _cpdl.WebViewFetchAsync = null;

        try { _downloader.Dispose(); } catch { }

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

            var cts = _downloadCts;
            var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress, file => _pausedQueueFiles.ContainsKey(file.DownloadUrl), cts?.Token ?? default);
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
        try
        {
            await RetryQueueItemAsync(queueItem);
        }
        catch (Exception ex)
        {
            Log($"❌ Error reanudando {queueItem.FileName}: {ex.Message}");
        }
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

        var cts = _downloadCts;
        var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress, null, cts?.Token ?? default);
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
    ///         Mutopia / OpenScore: descarga completa automática.
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
            Log("⚠️ Selecciona Mutopia, OpenScore, Musopen o Todas para descargar catálogo");
            return;
        }

        if (!_enableMutopia && source == "Mutopia")
        {
            Log("⏸️ Mutopia está desactivado por configuración. Usa otra fuente o Todas.");
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
        // NOTE: local to this run — ConcurrentDictionary avoids lock() in OnItem hot path
        // and eliminates the Clear()-vs-Add() race that existed when this was a field.
        var catalogSeenKeys = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

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
            if (_onlyClassical && !IsClassicalItem(item))
                return;

            var key = $"{item.Source}|{item.Title}|{item.Composer}";
            if (!catalogSeenKeys.TryAdd(key, 0)) return;  // lock-free O(1) dedup
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
            if (_enableMusopen && _musopen.HasApiKey && (source is "Musopen" or "Todas"))
                fetchTasks.Add(GuardedFetch("Musopen", _musopen.FetchAllAsync(OnItem, progress, ct), progress));
            if (_enableOpenScore && (source is "OpenScore" or "Todas"))
                fetchTasks.Add(GuardedFetch("OpenScore", _openScore.FetchAllAsync(OnItem, progress, ct), progress));
            if (source == "Todas")
            {
                var disabled = new List<string>();
                if (!_enableMutopia) disabled.Add("Mutopia");
                if (!_enableMusopen) disabled.Add("Musopen");
                if (!_enableOpenScore) disabled.Add("OpenScore");
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
                _circuitBreaker?.RecordSuccess(); // Fase 1 completó OK; preflight descartó fase 2
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

                                        // CPDL removido - LoadFiles deshabilitado
                                        loaded = false;  // await _cpdl.LoadFilesAsync(item, loadCts.Token).ConfigureAwait(false);

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
                                        // "CPDL" => _cpdl.GetSessionHeaders(),  // CPDL removido
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
            // _cpdl.FlushNoFilesBlacklist();  // CPDL removido
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
                    // CPDL removido - esta sección se salta
                    loaded = false;  // item.Source == "CPDL"
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
        // CPDL removido - método deshabilitado
        Log("ℹ️ CPDL ha sido desactivado. Usa Mutopia, OpenScore, Musopen u otras fuentes.");
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
        // CPDL removido - método deshabilitado
        return Task.FromResult(false);
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
            else if (source == "Musopen")
            {
                results = await FetchWithCacheAsync("Musopen", query,
                    (p, token) => _musopen.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "OpenScore")
            {
                results = await FetchWithCacheAsync("OpenScore", query,
                    (p, token) => _openScore.SearchAsync(query, p, token), progress, ct);
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
                var musopenTask = _enableMusopen && _musopen.HasApiKey
                    ? SafeSearchAsync("Musopen", () =>
                        FetchWithCacheAsync("Musopen", query, (p, token) => _musopen.SearchAsync(query, p, token), null, ct))
                    : Task.FromResult<List<PartituraItem>>([]);
                var openScoreTask = SafeSearchAsync("OpenScore", () =>
                    FetchWithCacheAsync("OpenScore", query, (p, token) => _openScore.SearchAsync(query, p, token), null, ct));

                var all = await Task.WhenAll(mutopiaTask, musopenTask, openScoreTask);
                results.AddRange(all[0]);
                results.AddRange(all[1]);
                results.AddRange(all[2]);
            }

            if (_onlyClassical)
                results = results.Where(IsClassicalItem).ToList();

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
        _onlyClassical = chkOnlyClassical.IsChecked == true;
        ApplyFilter();
        UpdateSourceDashboard();
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
        var mu = _enableMusopen ? (_musopen.HasSession ? "ON+🔐" : "ON") : "OFF";
        var c = _onlyClassical ? "ON" : "OFF";
        txtSourceStatus.Text = $"Fuentes M:{m} MO:{mu} C:{c}";
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
        _enableMusopen = chkEnableMusopen.IsChecked == true;
        _enableOpenScore = chkEnableOpenScore.IsChecked == true;

        // Mantener al menos una fuente activa para evitar corridas vacías.
        if (!_enableMutopia && !_enableMusopen && !_enableOpenScore)
        {
            _enableMutopia = true;
            chkEnableMutopia.IsChecked = true;
            Log("ℹ️ Debe quedar al menos una fuente activa; Mutopia reactivado.");
        }

        SaveUiState();
        UpdateSourceDashboard();
    }

    private void ChkAutoConvert_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        SaveUiState();
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
        bool onlyXml = chkOnlyXml.IsChecked == true;
        bool onlyClassical = chkOnlyClassical.IsChecked == true;
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
            if (onlyClassical && !IsClassicalItem(item)) return false;
            if (!string.IsNullOrWhiteSpace(tagFilter) && !item.UserTag.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(titleQuery) && !item.Title.Contains(titleQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(composerQuery) && !item.Composer.Contains(composerQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (onlyPdf || onlyMidi || onlyXml)
            {
                var hasPdf = item.Files.Any(f => string.Equals(f.Format, "PDF", StringComparison.OrdinalIgnoreCase));
                var hasMidi = item.Files.Any(f => string.Equals(f.Format, "MIDI", StringComparison.OrdinalIgnoreCase));
                var hasXml = item.Files.Any(f =>
                    string.Equals(f.Format, "XML", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Format, "MXL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Format, "MUSICXML", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Format, "MSCZ", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Format, "MSCX", StringComparison.OrdinalIgnoreCase));
                bool formatOk = (onlyPdf && hasPdf) || (onlyMidi && hasMidi) || (onlyXml && hasXml);
                if (!formatOk) return false;
            }
            return true;
        };
        _resultsView.View.Filter = filterPredicate;
        // View.Filter refreshes synchronously; enumerate the already-filtered view
        // instead of re-evaluating the predicate over _allResults a second time.
        _filtered = _resultsView.View.Cast<PartituraItem>().ToList();
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
        bool onlyXml = chkOnlyXml.IsChecked == true;

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

                    // CPDL LoadFiles removido
                    await Task.Delay(0, warmupCts.Token);  // placeholder

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
                    if (!onlyPdf && !onlyMidi && !onlyXml) return true;
                    var isPdf = string.Equals(f.Format, "PDF", StringComparison.OrdinalIgnoreCase);
                    var isMidi = string.Equals(f.Format, "MIDI", StringComparison.OrdinalIgnoreCase);
                    var isXml =
                        string.Equals(f.Format, "XML", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(f.Format, "MXL", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(f.Format, "MUSICXML", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(f.Format, "MSCZ", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(f.Format, "MSCX", StringComparison.OrdinalIgnoreCase);
                    return (onlyPdf && isPdf) || (onlyMidi && isMidi) || (onlyXml && isXml);
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
                    // "CPDL" => _cpdl.GetSessionHeaders(),  // CPDL removido
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
            if (chkAutoConvertOemer.IsChecked == true && result.Ok > 0 && !ct.IsCancellationRequested)
                await AutoConvertWithOemerAsync(destFolder);
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
        if (btnCancelVideo != null)
            btnCancelVideo.IsEnabled = false;
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

        var siblingSet = BuildMusicScoreSiblingSetCached(destFolder);
        var pendingBeforeCooldown = SafeEnumerateFilesCached(destFolder, f => AudiverisInputExtensions.Contains(Path.GetExtension(f)) && !HasMusicScoreSiblingFast(f, siblingSet))
            .Where(f => !_audiverisKnownPageFailures.ContainsKey(f))
            .ToList();
        var pending = FilterByCooldown(pendingBeforeCooldown, IsAudiverisFamilyInTimeoutCooldown, out var skippedCooldownFamilies, out var skippedCooldownFamilyKeys);

        if (skippedCooldownFamilies > 0)
            Log($"ℹ️ Auto-Audiveris: {skippedCooldownFamilies} archivo(s) omitidos por cooldown de timeout activo ({skippedCooldownFamilyKeys.Count} familia(s)){FormatTopCooldownFamilies(skippedCooldownFamilyKeys, _audiverisTimeoutFamilies)}.");

        if (pending.Count == 0)
        {
            Log("🎼 Auto-convertir: todo ya estaba convertido o no hay PDF/imagen.");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates, out var familySizes);
        if (skippedFamilyDuplicates > 0)
            Log($"ℹ️ Auto-Audiveris: {skippedFamilyDuplicates} archivo(s) omitidos por duplicado de familia (a4/let).");

        Log($"🎼 Auto-convertir: procesando {familyPending.Count} archivo(s) con Audiveris...");
        _audiverisLog.Clear();
        _audiverisRunning = true;
        _audiverisCts = new CancellationTokenSource();
        if (btnConvertAudiveris != null) btnConvertAudiveris.IsEnabled = false;
        if (btnCancelAudiveris != null) btnCancelAudiveris.IsEnabled = true;

        try
        {
            var r = await RunAudiverisBatchCoreAsync(familyPending, audiverisExe, "Auto-Audiveris", destFolder, _audiverisCts.Token, familySizes);
            var converted = r.Ok + r.Partial;
            var msg = $"🎼 Auto-Audiveris: {converted}/{familyPending.Count} convertidas" +
                      $" | ✅ {r.Ok} completas, ⚠️ {r.Partial} parciales, ❌ {r.Fail} sin salida" +
                      (r.SkippedByFamilyTimeout > 0 ? $", ⏭️ {r.SkippedByFamilyTimeout} omitidos" : string.Empty);
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
            _audiverisCts?.Dispose();
            _audiverisCts = null;
            if (btnConvertAudiveris != null) btnConvertAudiveris.IsEnabled = true;
            if (btnCancelAudiveris != null) btnCancelAudiveris.IsEnabled = false;
        }
    }

    private void BtnResetAudiverisCooldown_Click(object sender, RoutedEventArgs e)
    {
        var cooldownCount = _audiverisTimeoutFamilies.Count;
        var strikeCount = _audiverisTimeoutStrikes.Count;
        _audiverisTimeoutFamilies.Clear();
        _audiverisTimeoutStrikes.Clear();
        _audiverisStrikeLastUtc.Clear();
        _audiverisTimeoutFamiliesDirty = true;
        _audiverisTimeoutStrikesDirty = true;
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

        var inputs = SafeEnumerateFilesCached(destFolder, f => AudiverisInputExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        LogDebug($"🎼 Audiveris: candidatos detectados {inputs.Count}");

        if (inputs.Count == 0)
        {
            Log("🎼 Audiveris: no hay partituras PDF/imagen para convertir.");
            return;
        }

        var siblingSet = BuildMusicScoreSiblingSetCached(destFolder);
        var noSibling = inputs.Where(f => !HasMusicScoreSiblingFast(f, siblingSet)).ToList();
        var pendingBeforeCooldown = new List<string>(noSibling.Count);
        var skippedKnownPageFails = 0;
        foreach (var file in noSibling)
        {
            if (_audiverisKnownPageFailures.ContainsKey(file))
            {
                skippedKnownPageFails++;
                continue;
            }

            pendingBeforeCooldown.Add(file);
        }
        var pending = FilterByCooldown(pendingBeforeCooldown, IsAudiverisFamilyInTimeoutCooldown, out var skippedCooldownFamilies, out var skippedCooldownFamilyKeys);

        if (skippedKnownPageFails > 0)
            Log($"ℹ️ Audiveris: {skippedKnownPageFails} archivo(s) omitidos por PAGE fail previo en esta sesión.");
        if (skippedCooldownFamilies > 0)
            Log($"ℹ️ Audiveris: {skippedCooldownFamilies} archivo(s) omitidos por cooldown de timeout activo ({skippedCooldownFamilyKeys.Count} familia(s)){FormatTopCooldownFamilies(skippedCooldownFamilyKeys, _audiverisTimeoutFamilies)}.");

        if (pending.Count == 0)
        {
            Log("🎼 Audiveris: todo ya estaba convertido (MXL/XML/MSCZ/MSCX).");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates, out var familySizes);
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

        var effectiveAudiverisParallel = GetEffectiveAudiverisParallel();
        Log($"🎼 Audiveris: inicio conversion de {batch.Count} archivo(s) (paralelo={effectiveAudiverisParallel}, cfg={_audiverisParallel})." + (deferred > 0 ? $" ({deferred} quedan pendientes)" : string.Empty));
        _audiverisLog.Clear();

        _audiverisRunning = true;
        _audiverisCts = new CancellationTokenSource();
        btnConvertAudiveris.IsEnabled = false;
        btnCancelAudiveris.IsEnabled = true;
        btnSearch.IsEnabled = false;
        btnDownload.IsEnabled = false;
        txtStatus.Text = "🎼 Convirtiendo con Audiveris...";
        var capturedDestFolder = txtDestFolder.Text?.Trim() ?? string.Empty;

        try
        {
            var r = await RunAudiverisBatchCoreAsync(batch, audiverisExe, "Audiveris", capturedDestFolder, _audiverisCts.Token, familySizes);
            var converted = r.Ok + r.Partial;
            var statusMsg = $"🎼 Audiveris: {converted}/{batch.Count} convertidas" +
                            $" | ✅ {r.Ok} completas, ⚠️ {r.Partial} parciales, ❌ {r.Fail} sin salida" +
                            (r.SkippedByFamilyTimeout > 0 ? $", ⏭️ {r.SkippedByFamilyTimeout} omitidos" : string.Empty);
            txtStatus.Text = statusMsg;
            Log(statusMsg + (deferred > 0 ? $" | pendientes: {deferred}" : string.Empty));
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
            _audiverisCts?.Dispose();
            _audiverisCts = null;
            btnConvertAudiveris.IsEnabled = true;
            btnCancelAudiveris.IsEnabled = false;
            btnSearch.IsEnabled = true;
            UpdateDownloadButton();
        }
    }

    private async Task<(int Ok, int Partial, int Fail, int SkippedByFamilyTimeout)> RunAudiverisBatchCoreAsync(
        IList<string> batch,
        string audiverisExe,
        string label,
        string fallbackOutputDir,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, long>? fileSizes = null)
    {
        int ok = 0, partial = 0, fail = 0, skippedByFamilyTimeout = 0, processed = 0;
        int total = batch.Count;
        var siblingSet = BuildMusicScoreSiblingSetCached(fallbackOutputDir);
        var effectiveParallel = GetEffectiveAudiverisParallel();

        try
        {
            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions { MaxDegreeOfParallelism = effectiveParallel, CancellationToken = ct },
                async (input, cancellationToken) =>
                {
                    var name = Path.GetFileName(input);
                    var familyKey = NormalizeAudiverisFamilyKey(input);
                    var knownSize = fileSizes is not null && fileSizes.TryGetValue(input, out var sz) ? sz : -1L;

                    var idx = Interlocked.Increment(ref processed);
                    if (idx == 1 || idx == total || idx % 5 == 0)
                    {
                        await Dispatcher.InvokeAsync(() =>
                            txtStatus.Text = $"🎼 {label} [{idx}/{total}] {name}");
                    }
                    LogDebug($"{label} [{idx}/{total}] {name}");

                    var computedTimeout = ComputeAudiverisTimeoutSeconds(input, knownSize);
                    var sw = Stopwatch.StartNew();
                    (bool Success, bool Partial, bool PageFailure) result;
                    try
                    {
                        result = await RunAudiverisConversionAsync(audiverisExe, input, fallbackOutputDir, allowSiblingFallback: true, siblingSet, knownSize).ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref fail);
                        MarkAudiverisFamilyTimeout(input, familyKey);
                        _ = Task.Run(() => AppendTelemetry(_audiverisTelemetryPath, new TimeoutTelemetryEntry { FamilyKey = familyKey, ComputedTimeoutSeconds = computedTimeout, ActualElapsedSeconds = sw.Elapsed.TotalSeconds, TimedOut = true, DateUtc = DateTime.UtcNow }));
                        var snapOk = Volatile.Read(ref ok); var snapPart = Volatile.Read(ref partial); var snapFail = Volatile.Read(ref fail);
                        Log($"❌ {label} timeout [{idx}/{total}] {name}: {tex.Message} (ok={snapOk}, parcial={snapPart}, fail={snapFail})");
                        if (idx % 5 == 0)
                        {
                            var conv = snapOk + snapPart;
                            Log($"📊 {label} progreso: {idx}/{total} procesadas, convertidas={conv} ({conv * 100 / Math.Max(1, total)}%) (ok={snapOk}, parcial={snapPart}, fail={snapFail}){(conv == 0 ? " | ⚠️ 0 convertidas aún" : string.Empty)}");
                        }
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "⏱️ Timeout" }));
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref fail);
                        var snapOk = Volatile.Read(ref ok); var snapFail = Volatile.Read(ref fail);
                        Log($"❌ {label} error inesperado [{idx}/{total}] {name}: {ex.GetType().Name}: {ex.Message} (ok={snapOk}, fail={snapFail})");
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "❌ Error" }));
                        return;
                    }

                    sw.Stop();
                    if (result.Success)
                    {
                        _ = Task.Run(() => AppendTelemetry(_audiverisTelemetryPath, new TimeoutTelemetryEntry { FamilyKey = familyKey, ComputedTimeoutSeconds = computedTimeout, ActualElapsedSeconds = sw.Elapsed.TotalSeconds, TimedOut = false, DateUtc = DateTime.UtcNow }));
                        if (_audiverisTimeoutFamilies.TryRemove(familyKey, out _))
                            _audiverisTimeoutFamiliesDirty = true;
                        if (_audiverisTimeoutStrikes.TryRemove(familyKey, out _))
                            _audiverisTimeoutStrikesDirty = true;
                        if (_audiverisStrikeLastUtc.TryRemove(familyKey, out _))
                            _audiverisTimeoutStrikesDirty = true;
                        Interlocked.Increment(ref ok);
                        var snapOk = Volatile.Read(ref ok); var snapPart = Volatile.Read(ref partial); var snapFail = Volatile.Read(ref fail);
                        Log($"✅ Convertido [{idx}/{total}]: {name} (ok={snapOk}, parcial={snapPart}, fail={snapFail})");
                        await Dispatcher.InvokeAsync(() =>
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = "✅ Convertido" }));
                    }
                    else
                    {
                        Interlocked.Increment(ref fail);
                        var movedPath = MoveToAudiverisFailedFolder(input);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (result.PageFailure)
                            {
                                if (_audiverisKnownPageFailures.TryAdd(movedPath ?? input, 0))
                                    _audiverisKnownPageFailuresDirty = true;
                            }
                            var statusLabel = result.PageFailure ? "❌ PAGE fail" : "⚠️ Sin salida";
                            var moved = movedPath != null ? " → _AudiverisFailed" : string.Empty;
                            var snapOk2 = Volatile.Read(ref ok); var snapPart2 = Volatile.Read(ref partial); var snapFail2 = Volatile.Read(ref fail);
                            Log($"{statusLabel} [{idx}/{total}]: {name}{moved} (ok={snapOk2}, parcial={snapPart2}, fail={snapFail2})");
                            _audiverisLog.Add(new AudiverisLogItem { FileName = name, Status = statusLabel + moved });
                        });
                    }

                    if (idx % 5 == 0)
                    {
                        var snapOk = Volatile.Read(ref ok); var snapPart = Volatile.Read(ref partial); var snapFail = Volatile.Read(ref fail);
                        var conv = snapOk + snapPart;
                        Log($"📊 {label} progreso: {idx}/{total} procesadas, convertidas={conv} ({conv * 100 / Math.Max(1, total)}%) (ok={snapOk}, parcial={snapPart}, fail={snapFail}){(conv == 0 ? " | ⚠️ 0 convertidas aún" : string.Empty)}");
                    }
                }).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Log($"⏹️ {label} cancelado por usuario (ok={ok}, parcial={partial}, fail={fail}).");
        }

        SaveAudiverisPageFailures();
        SaveAudiverisTimeoutFamilies();
        SaveAudiverisTimeoutStrikes();
        InvalidateRawFilesCache(fallbackOutputDir);
        UpdateAudiverisStatus();

        return (ok, partial, fail, skippedByFamilyTimeout);
    }

    private void BtnCancelAudiveris_Click(object sender, RoutedEventArgs e)
    {
        _audiverisCts?.Cancel();
        if (btnCancelAudiveris != null) btnCancelAudiveris.IsEnabled = false;
        Log("⏹️ Cancelando Audiveris...");
    }

    // ── oemer handlers ─────────────────────────────────────────────────────

    private void BtnCancelOemer_Click(object sender, RoutedEventArgs e)
    {
        _oemerCts?.Cancel();
        if (btnCancelOemer != null) btnCancelOemer.IsEnabled = false;
        Log("⏹️ Cancelando oemer...");
    }

    private void BtnResetOemerCooldown_Click(object sender, RoutedEventArgs e)
    {
        var cooldownCount = _oemerTimeoutFamilies.Count;
        var strikeCount = _oemerTimeoutStrikes.Count;
        _oemerTimeoutFamilies.Clear();
        _oemerTimeoutStrikes.Clear();
        _oemerStrikeLastUtc.Clear();
        _oemerTimeoutFamiliesDirty = true;
        _oemerTimeoutStrikesDirty = true;
        SaveOemerTimeoutFamilies();
        SaveOemerTimeoutStrikes();
        UpdateOemerStatus();
        Log($"🔄 oemer: cooldown y strikes reiniciados ({cooldownCount} familias, {strikeCount} strikes).");
    }

    private async void BtnConvertOemer_Click(object sender, RoutedEventArgs e)
    {
        Log("🎵 Botón oemer pulsado.");
        if (_videoRunning) { Log("⚠️ Hay un vídeo en proceso. Espera a que termine."); return; }

        var destFolder = txtDestFolder.Text?.Trim();
        if (string.IsNullOrWhiteSpace(destFolder) || !Directory.Exists(destFolder))
        {
            Log("⚠️ oemer: carpeta destino no válida.");
            DarkDialogService.ShowMessage(this, "Selecciona una carpeta de destino válida.", "oemer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (oemerExe, prefixArgs) = ResolveOemerCommand();
        if (string.IsNullOrWhiteSpace(oemerExe))
        {
            Log("⚠️ oemer no encontrado. Instala con: pip install oemer");
            DarkDialogService.ShowMessage(this,
                "No se encontró oemer. Instálalo con:\n  pip install oemer\n\nO define OEMER_EXE con la ruta al ejecutable.",
                "oemer no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var inputs = SafeEnumerateFilesCached(destFolder, f => AudiverisInputExtensions.Contains(Path.GetExtension(f)))
            .ToList();
        if (inputs.Count == 0)
        {
            Log("🎵 oemer: no hay partituras PDF/imagen para convertir.");
            return;
        }

        var siblingSet = BuildMusicScoreSiblingSetCached(destFolder);
        var noSibling = inputs.Where(f => !HasMusicScoreSiblingFast(f, siblingSet)).ToList();
        var pendingBeforeCooldown = new List<string>(noSibling.Count);
        var skippedKnownFails = 0;
        foreach (var file in noSibling)
        {
            if (_oemerKnownPageFailures.ContainsKey(file))
            {
                skippedKnownFails++;
                continue;
            }

            pendingBeforeCooldown.Add(file);
        }
        var pending = FilterByCooldown(pendingBeforeCooldown, IsOemerFamilyInTimeoutCooldown, out var skippedCooldown, out var skippedCooldownFamilyKeys);

        var pdfRenderer = ResolvePdfToPngRenderer();
        if (pdfRenderer is null)
        {
            var skippedMissingPdfConverter = pending.Count(IsPdfInput);
            if (skippedMissingPdfConverter > 0)
            {
                pending = pending.Where(f => !IsPdfInput(f)).ToList();
                Log($"⚠️ oemer: {skippedMissingPdfConverter} PDF omitidos; falta convertidor PDF→PNG (Ghostscript/Poppler).");
            }
        }

        if (skippedKnownFails > 0)
            Log($"ℹ️ oemer: {skippedKnownFails} archivo(s) omitidos por fallo permanente previo.");
        if (skippedCooldown > 0)
            Log($"ℹ️ oemer: {skippedCooldown} archivo(s) omitidos por cooldown de timeout activo ({skippedCooldownFamilyKeys.Count} familia(s)){FormatTopCooldownFamilies(skippedCooldownFamilyKeys, _oemerTimeoutFamilies)}.");

        if (pending.Count == 0)
        {
            Log("🎵 oemer: todo ya estaba convertido.");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates, out _);
        if (skippedFamilyDuplicates > 0)
            Log($"ℹ️ oemer: {skippedFamilyDuplicates} archivo(s) omitidos por duplicado de familia (a4/let).");

        var batch = familyPending.Take(_oemerBatchSize).ToList();
        var deferred = familyPending.Count - batch.Count;

        var proceed = DarkDialogService.ShowMessage(
            this,
            $"Se convertirán {batch.Count} archivo(s) con oemer.\n\nSolo se procesan los que aún no tienen salida MusicXML." +
            (deferred > 0 ? $"\n\nQuedarán {deferred} pendientes para la siguiente corrida." : string.Empty) +
            "\n\n¿Continuar?",
            "Convertir con oemer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (proceed != MessageBoxResult.Yes)
        {
            Log("ℹ️ oemer cancelado por usuario.");
            return;
        }

        var effectiveOemerParallel = GetEffectiveOemerParallel();
        Log($"🎵 oemer: inicio conversión de {batch.Count} archivo(s) (paralelo={effectiveOemerParallel}, cfg={_oemerParallel})." + (deferred > 0 ? $" ({deferred} quedan pendientes)" : string.Empty));
        _oemerLog.Clear();

        _oemerRunning = true;
        _oemerCts = new CancellationTokenSource();
        btnConvertOemer.IsEnabled = false;
        btnCancelOemer.IsEnabled = true;
        btnSearch.IsEnabled = false;
        btnDownload.IsEnabled = false;
        txtStatus.Text = "🎵 Convirtiendo con oemer...";

        try
        {
            var r = await RunOemerBatchCoreAsync(batch, oemerExe, prefixArgs, "oemer", destFolder, _oemerCts.Token);
            var statusMsg = $"🎵 oemer: {r.Ok}/{batch.Count} convertidas | ✅ {r.Ok}, ❌ {r.Fail}";
            txtStatus.Text = statusMsg;
            Log(statusMsg + (deferred > 0 ? $" | pendientes: {deferred}" : string.Empty));
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error en conversión oemer";
            Log($"❌ oemer error: {ex.Message}");
            DarkDialogService.ShowMessage(this, $"Error en conversión con oemer: {ex.Message}", "oemer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _oemerRunning = false;
            _oemerCts?.Dispose();
            _oemerCts = null;
            btnConvertOemer.IsEnabled = true;
            btnCancelOemer.IsEnabled = false;
            btnSearch.IsEnabled = true;
            UpdateDownloadButton();
        }
    }

    private async Task AutoConvertWithOemerAsync(string destFolder)
    {
        if (_oemerRunning) return;

        var (oemerExe, prefixArgs) = ResolveOemerCommand();
        if (string.IsNullOrWhiteSpace(oemerExe))
        {
            Log("⚠️ Auto-oemer: oemer no encontrado. Instala con: pip install oemer");
            return;
        }

        var siblingSet = BuildMusicScoreSiblingSetCached(destFolder);
        var pendingBeforeCooldown = SafeEnumerateFilesCached(destFolder,
                                f => AudiverisInputExtensions.Contains(Path.GetExtension(f))
                  && !HasMusicScoreSiblingFast(f, siblingSet))
            .Where(f => !_oemerKnownPageFailures.ContainsKey(f))
            .ToList();
        var pending = FilterByCooldown(pendingBeforeCooldown, IsOemerFamilyInTimeoutCooldown, out var skippedCooldown, out var skippedCooldownFamilyKeys);

        var pdfRenderer = ResolvePdfToPngRenderer();
        if (pdfRenderer is null)
        {
            var skippedMissingPdfConverter = pending.Count(IsPdfInput);
            if (skippedMissingPdfConverter > 0)
            {
                pending = pending.Where(f => !IsPdfInput(f)).ToList();
                Log($"⚠️ Auto-oemer: {skippedMissingPdfConverter} PDF omitidos; falta convertidor PDF→PNG (Ghostscript/Poppler).");
            }
        }

        if (skippedCooldown > 0)
            Log($"ℹ️ Auto-oemer: {skippedCooldown} archivo(s) omitidos por cooldown activo ({skippedCooldownFamilyKeys.Count} familia(s)){FormatTopCooldownFamilies(skippedCooldownFamilyKeys, _oemerTimeoutFamilies)}.");

        if (pending.Count == 0)
        {
            Log("🎵 Auto-oemer: todo ya estaba convertido o no hay PDF/imagen.");
            return;
        }

        var familyPending = SelectFirstPerAudiverisFamily(pending, out var skippedFamilyDuplicates, out _);
        if (skippedFamilyDuplicates > 0)
            Log($"ℹ️ Auto-oemer: {skippedFamilyDuplicates} archivo(s) omitidos por duplicado de familia (a4/let).");

        Log($"🎵 Auto-oemer: procesando {familyPending.Count} archivo(s)...");
        _oemerLog.Clear();
        _oemerRunning = true;
        _oemerCts = new CancellationTokenSource();
        if (btnConvertOemer != null) btnConvertOemer.IsEnabled = false;
        if (btnCancelOemer != null) btnCancelOemer.IsEnabled = true;

        try
        {
            var r = await RunOemerBatchCoreAsync(familyPending, oemerExe, prefixArgs, "Auto-oemer", destFolder, _oemerCts.Token);
            var msg = $"🎵 Auto-oemer: {r.Ok}/{familyPending.Count} convertidas | ✅ {r.Ok}, ❌ {r.Fail}";
            txtStatus.Text = msg;
            Log(msg);
        }
        catch (Exception ex)
        {
            Log($"❌ Auto-oemer error: {ex.Message}");
        }
        finally
        {
            _oemerRunning = false;
            _oemerCts?.Dispose();
            _oemerCts = null;
            if (btnConvertOemer != null) btnConvertOemer.IsEnabled = true;
            if (btnCancelOemer != null) btnCancelOemer.IsEnabled = false;
        }
    }

    private async Task<(int Ok, int Fail)> RunOemerBatchCoreAsync(
        IList<string> batch,
        string oemerExe,
        string[] prefixArgs,
        string label,
        string rootDir,
        CancellationToken ct = default)
    {
        int ok = 0, fail = 0, processed = 0;
        int total = batch.Count;
        var effectiveParallel = GetEffectiveOemerParallel();

        try
        {
            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions { MaxDegreeOfParallelism = effectiveParallel, CancellationToken = ct },
                async (input, innerCt) =>
                {
                    var name = Path.GetFileName(input);
                    var familyKey = NormalizeAudiverisFamilyKey(input);
                    var idx = Interlocked.Increment(ref processed);
                    if (idx == 1 || idx == total || idx % 5 == 0)
                    {
                        await Dispatcher.InvokeAsync(() =>
                            txtStatus.Text = $"🎵 {label} [{idx}/{total}] {name}");
                    }
                    LogDebug($"{label} [{idx}/{total}] {name}");

                    var computedTimeout = ComputeOemerTimeoutSeconds(input);
                    var sw = Stopwatch.StartNew();
                    (bool Success, bool PermanentFailure) result;
                    try
                    {
                        result = await RunOemerConversionAsync(oemerExe, prefixArgs, input, innerCt).ConfigureAwait(false);
                    }
                    catch (TimeoutException tex)
                    {
                        sw.Stop();
                        Interlocked.Increment(ref fail);
                        MarkOemerFamilyTimeout(input, familyKey);
                        _ = Task.Run(() => AppendTelemetry(_oemerTelemetryPath, new TimeoutTelemetryEntry { FamilyKey = familyKey, ComputedTimeoutSeconds = computedTimeout, ActualElapsedSeconds = sw.Elapsed.TotalSeconds, TimedOut = true, DateUtc = DateTime.UtcNow }));
                        var snapOk = Volatile.Read(ref ok); var snapFail = Volatile.Read(ref fail);
                        Log($"❌ {label} timeout [{idx}/{total}] {name}: {tex.Message} (ok={snapOk}, fail={snapFail})");
                        await Dispatcher.InvokeAsync(() =>
                            _oemerLog.Add(new AudiverisLogItem { FileName = name, Status = "⏱️ Timeout" }));
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref fail);
                        Log($"❌ {label} error [{idx}/{total}] {name}: {ex.GetType().Name}: {ex.Message}");
                        await Dispatcher.InvokeAsync(() =>
                            _oemerLog.Add(new AudiverisLogItem { FileName = name, Status = "❌ Error" }));
                        return;
                    }

                    sw.Stop();
                    if (result.Success)
                    {
                        _ = Task.Run(() => AppendTelemetry(_oemerTelemetryPath, new TimeoutTelemetryEntry { FamilyKey = familyKey, ComputedTimeoutSeconds = computedTimeout, ActualElapsedSeconds = sw.Elapsed.TotalSeconds, TimedOut = false, DateUtc = DateTime.UtcNow }));
                        if (_oemerTimeoutFamilies.TryRemove(familyKey, out _))
                            _oemerTimeoutFamiliesDirty = true;
                        if (_oemerTimeoutStrikes.TryRemove(familyKey, out _))
                            _oemerTimeoutStrikesDirty = true;
                        if (_oemerStrikeLastUtc.TryRemove(familyKey, out _))
                            _oemerTimeoutStrikesDirty = true;
                        Interlocked.Increment(ref ok);
                        var snapOk2 = Volatile.Read(ref ok); var snapFail2 = Volatile.Read(ref fail);
                        Log($"✅ oemer [{idx}/{total}]: {name} (ok={snapOk2}, fail={snapFail2})");
                        await Dispatcher.InvokeAsync(() =>
                            _oemerLog.Add(new AudiverisLogItem { FileName = name, Status = "✅ Convertido" }));
                    }
                    else
                    {
                        Interlocked.Increment(ref fail);
                        if (result.PermanentFailure)
                        {
                            Log($"🚫 oemer fallo permanente [{idx}/{total}]: {name}");
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (_oemerKnownPageFailures.TryAdd(input, 0))
                                    _oemerKnownPageFailuresDirty = true;
                                _oemerLog.Add(new AudiverisLogItem { FileName = name, Status = "🚫 Fallo permanente" });
                            });
                        }
                        else
                        {
                            var movedPath = MoveToOemerFailedFolder(input);
                            var moved = movedPath != null ? " → _OemerFailed" : string.Empty;
                            Log($"⚠️ Sin salida [{idx}/{total}]: {name}{moved}");
                            await Dispatcher.InvokeAsync(() =>
                                _oemerLog.Add(new AudiverisLogItem { FileName = name, Status = "⚠️ Sin salida" + moved }));
                        }
                    }

                    if (idx % 5 == 0)
                    {
                        var snapOk3 = Volatile.Read(ref ok); var snapFail3 = Volatile.Read(ref fail);
                        Log($"📊 {label} progreso: {idx}/{total} · convertidas={snapOk3} ({snapOk3 * 100 / Math.Max(1, total)}%) · fail={snapFail3}");
                    }
                }).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Log($"⏹️ {label} cancelado (ok={ok}, fail={fail}).");
        }

        SaveOemerPageFailures();
        SaveOemerTimeoutFamilies();
        SaveOemerTimeoutStrikes();
        InvalidateRawFilesCache(rootDir);
        UpdateOemerStatus();

        return (ok, fail);
    }

    private static (string Exe, string[] PrefixArgs) ResolveOemerCommand()
    {
        lock (_oemerCommandCacheLock)
        {
            if (_oemerCommandCacheInitialized) return _oemerCommandCache ?? (string.Empty, []);
            _oemerCommandCache = ResolveOemerCommandCore();
            _oemerCommandCacheInitialized = true;
            return _oemerCommandCache ?? (string.Empty, []);
        }
    }

    private static (string Exe, string[] PrefixArgs)? ResolveOemerCommandCore()
    {
        // 1. Env var OEMER_EXE
        var fromEnv = Environment.GetEnvironmentVariable("OEMER_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return (fromEnv, []);

        // 2. oemer.exe / oemer.cmd on PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in new[] { "oemer.exe", "oemer.cmd" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return (p, []);
            }
        }

        // 3. Python Scripts dirs (pip install oemer puts oemer.exe there)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scriptsDirs = new List<string>();
        foreach (var pyVer in new[] { "Python313", "Python312", "Python311", "Python310", "Python39", "Python38" })
        {
            scriptsDirs.Add(Path.Combine(home, "AppData", "Local", "Programs", "Python", pyVer, "Scripts"));
            scriptsDirs.Add(Path.Combine(home, "AppData", "Roaming", "Python", pyVer, "Scripts"));
        }
        scriptsDirs.Add(Path.Combine(home, "AppData", "Local", "Programs", "Python", "Scripts"));
        scriptsDirs.Add(@"C:\ProgramData\miniconda3\Scripts");
        scriptsDirs.Add(@"C:\ProgramData\anaconda3\Scripts");
        scriptsDirs.Add(Path.Combine(home, "miniconda3", "Scripts"));
        scriptsDirs.Add(Path.Combine(home, "anaconda3", "Scripts"));

        foreach (var dir in scriptsDirs)
        {
            foreach (var name in new[] { "oemer.exe", "oemer.cmd" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return (p, []);
            }
        }

        // 4. venvs locales relativos al exe y al directorio de trabajo
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var cwdDir = Directory.GetCurrentDirectory();
        var venvRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { appDir, cwdDir })
        {
            var cur = root;
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(cur)) break;
                foreach (var venvName in new[] { ".venv", "venv", "env", ".env" })
                    venvRoots.Add(Path.Combine(cur, venvName, "Scripts"));
                cur = Path.GetDirectoryName(cur)!;
            }
        }
        foreach (var dir in venvRoots)
        {
            foreach (var name in new[] { "oemer.exe", "oemer.cmd" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return (p, []);
            }
        }

        return null;
    }

    private async Task<(bool Success, bool PermanentFailure)> RunOemerConversionAsync(
        string oemerExe,
        string[] prefixArgs,
        string inputPath,
        CancellationToken ct = default)
    {
        var isPdf = string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase);
        if (isPdf)
            return await RunOemerOnPdfAsync(oemerExe, prefixArgs, inputPath, ct).ConfigureAwait(true);

        return await RunOemerOnImageAsync(oemerExe, prefixArgs, inputPath, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Convierte PDF a PNG página a página con Ghostscript / pdftoppm / pymupdf,
    /// luego ejecuta oemer en cada PNG. Los MXL resultantes van al directorio del PDF
    /// renombrados como {stem}_p{n:0000}.mxl.
    /// </summary>
    private async Task<(bool Success, bool PermanentFailure)> RunOemerOnPdfAsync(
        string oemerExe, string[] prefixArgs, string pdfPath, CancellationToken ct)
    {
        var name = Path.GetFileName(pdfPath);
        var pdfDir = Path.GetDirectoryName(pdfPath) ?? string.Empty;
        var pdfStem = Path.GetFileNameWithoutExtension(pdfPath);
        var pdfFamilyKey = NormalizeAudiverisFamilyKey(pdfPath);
        long knownPdfSizeBytes;
        try { knownPdfSizeBytes = new FileInfo(pdfPath).Length; }
        catch { knownPdfSizeBytes = -1; }

        var renderer = ResolvePdfToPngRenderer();
        if (renderer is null)
        {
            Log($"⚠️ oemer PDF ({name}): no hay convertidor PDF→PNG. Instala Ghostscript (gswin64c.exe) o Poppler (pdftoppm.exe). Fichero no movido.");
            return (false, false); // no permanente: puede instalarse después
        }

        var tempDirBase = Path.Combine(Path.GetTempPath(), "scoredown_oemer");
        var tempDir = Path.Combine(tempDirBase, $"{Process.GetCurrentProcess().Id}_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            LogDebug($"🎵 oemer PDF ({name}): convirtiendo con {renderer.Value.Kind}…");
            var pages = await ConvertPdfToPngPagesAsync(pdfPath, tempDir, renderer.Value, 300, ct).ConfigureAwait(true);
            if (pages.Count == 0)
            {
                Log($"⚠️ oemer PDF ({name}): {renderer.Value.Kind} no produjo páginas PNG.");
                return (false, false);
            }

            Log($"🎵 oemer PDF ({name}): {pages.Count} página(s) → ejecutando oemer…");
            int okPages = 0;
            for (int pageIdx = 0; pageIdx < pages.Count; pageIdx++)
            {
                ct.ThrowIfCancellationRequested();
                var pagePng = pages[pageIdx];
                var pageNum = pageIdx + 1;
                var result = await RunOemerOnImageAsync(
                    oemerExe,
                    prefixArgs,
                    pagePng,
                    ct,
                    isFromPdf: true,
                    pdfPageCount: pages.Count,
                    timeoutFamilyKey: pdfFamilyKey,
                    knownSizeBytes: knownPdfSizeBytes).ConfigureAwait(true);
                if (result.Success)
                {
                    // Mover MXL al dir del PDF: stem_p0001.mxl
                    foreach (var ext in AudiverisOutputExtensions)
                    {
                        var pageStem = Path.GetFileNameWithoutExtension(pagePng);
                        var src = Path.Combine(tempDir, pageStem + ext);
                        if (!File.Exists(src)) continue;
                        var dst = Path.Combine(pdfDir, $"{pdfStem}_p{pageNum:0000}{ext}");
                        try { File.Move(src, dst, overwrite: true); okPages++; } catch { }
                    }
                }
                else
                {
                    LogDebug($"🎵 oemer PDF ({name}) pág {pageNum}: sin salida (permanent={result.PermanentFailure}).");
                }
            }

            if (okPages > 0)
            {
                Log($"✅ oemer PDF ({name}): {okPages}/{pages.Count} página(s) convertidas.");
                return (true, false);
            }
            Log($"⚠️ oemer PDF ({name}): ninguna página produjo MXL.");
            return (false, false);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
                // Limpiar dir padre si está vacío
                try { var parent = Path.GetDirectoryName(tempDir); if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent); } catch { }
            }
            catch { }
        }
    }

    /// <summary>Ejecuta oemer sobre un archivo de imagen (no PDF). Lógica original.</summary>
    private async Task<(bool Success, bool PermanentFailure)> RunOemerOnImageAsync(
        string oemerExe, string[] prefixArgs, string inputPath, CancellationToken ct, bool isFromPdf = false, int pdfPageCount = 1, string? timeoutFamilyKey = null, long knownSizeBytes = -1)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var timeoutSeconds = ComputeOemerTimeoutSeconds(inputPath, isFromPdf, pdfPageCount, timeoutFamilyKey, knownSizeBytes);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var name = Path.GetFileName(inputPath);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = oemerExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var ortThreads = ReadFeatureFlagInt("SCOREDOWN_OEMER_ORT_THREADS", 0, 0, 64);
        if (ortThreads == 0)
        {
            var logical = Environment.ProcessorCount;
            var parallel = Math.Max(1, _oemerParallel);
            ortThreads = Math.Max(1, logical / parallel);
        }
        psi.Environment["OEMER_ORT_THREADS"] = ortThreads.ToString();
        foreach (var a in prefixArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(inputPath);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outputDir);

        LogDebug($"🎵 oemer spawn ({name}): timeout={timeoutSeconds}s exe={oemerExe}");

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"No se pudo iniciar oemer: {oemerExe}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync(ct);
        var allDone = Task.WhenAll(waitTask, stdoutTask, stderrTask);

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var delayTask = Task.Delay(timeout, delayCts.Token);
        var completed = await Task.WhenAny(allDone, delayTask).ConfigureAwait(true);
        if (completed == delayTask)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await allDone.ConfigureAwait(true); } catch { }
            throw new TimeoutException($"oemer timeout tras {timeout.TotalMinutes:0.#} min ({timeoutSeconds}s)");
        }

        delayCts.Cancel();
        try
        {
            await allDone.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(true); } catch { }
            throw;
        }

        var stdout = stdoutTask.Result.Trim();
        var stderr = stderrTask.Result.Trim();
        LogDebug($"🎵 oemer exit ({name}): code={process.ExitCode}");

        if (process.ExitCode == 0 && HasMusicScoreSibling(inputPath))
            return (true, false);

        var combined = (stdout + "\n" + stderr).Trim();
        var isPermanent = IsOemerPermanentFailure(combined);
        var tail = combined.Length > 500 ? "..." + combined[^500..] : combined;
        Log($"⚠️ oemer fallo ({name}): exit={process.ExitCode} {tail}");
        return (false, isPermanent);
    }

    private static bool IsOemerPermanentFailure(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return false;
        return output.Contains("No staff", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("no staffs", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("Empty page", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("no notes detected", StringComparison.OrdinalIgnoreCase) ||
               // oemer pasó PDF directamente sin conversión previa (UnidentifiedImageError)
               output.Contains("UnidentifiedImageError", StringComparison.OrdinalIgnoreCase);
    }

    private static string? MoveToOemerFailedFolder(string inputPath)
    {
        try
        {
            if (!File.Exists(inputPath)) return null;
            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrEmpty(dir)) return null;
            if (Path.GetFileName(dir).Equals("_OemerFailed", StringComparison.OrdinalIgnoreCase))
                return inputPath;
            var failedDir = Path.Combine(dir, "_OemerFailed");
            Directory.CreateDirectory(failedDir);
            var dest = Path.Combine(failedDir, Path.GetFileName(inputPath));
            File.Move(inputPath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPdfInput(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Busca un ejecutable capaz de renderizar PDF → PNG.
    /// Devuelve ("gs", exe) para Ghostscript, ("pdftoppm", exe) para Poppler,
    /// ("pymupdf", python_exe) para pymupdf en el venv, o null si no hay nada disponible.
    /// </summary>
    private static (string Kind, string Exe)? ResolvePdfToPngRenderer()
    {
        lock (_pdfRendererCacheLock)
        {
            if (_pdfRendererCacheInitialized)
                return _pdfRendererCache;

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathDirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            // 1. Ghostscript en PATH
            foreach (var dir in pathDirs)
            {
                foreach (var name in new[] { "gswin64c.exe", "gswin32c.exe", "gs.exe" })
                {
                    var p = Path.Combine(dir, name);
                    if (File.Exists(p))
                    {
                        _pdfRendererCache = ("gs", p);
                        _pdfRendererCacheInitialized = true;
                        return _pdfRendererCache;
                    }
                }
            }

            // 2. Ghostscript en ubicaciones típicas de instalación Windows
            var pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (var root in new[] { pf64, pf86, @"C:\Program Files", @"C:\Program Files (x86)" })
            {
                var gsDir = Path.Combine(root, "gs");
                if (!Directory.Exists(gsDir)) continue;
                try
                {
                    foreach (var vDir in Directory.EnumerateDirectories(gsDir))
                    {
                        foreach (var name in new[] { "gswin64c.exe", "gswin32c.exe" })
                        {
                            var p = Path.Combine(vDir, "bin", name);
                            if (File.Exists(p))
                            {
                                _pdfRendererCache = ("gs", p);
                                _pdfRendererCacheInitialized = true;
                                return _pdfRendererCache;
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. pdftoppm (Poppler) en PATH
            foreach (var dir in pathDirs)
            {
                var p = Path.Combine(dir, "pdftoppm.exe");
                if (File.Exists(p))
                {
                    _pdfRendererCache = ("pdftoppm", p);
                    _pdfRendererCacheInitialized = true;
                    return _pdfRendererCache;
                }
            }
            // pdftoppm en MiKTeX / TeX Live / Poppler-windows
            foreach (var root in new[] { @"C:\Program Files\MiKTeX\miktex\bin\x64", @"C:\texlive\bin\win32", @"C:\poppler\bin" })
            {
                var p = Path.Combine(root, "pdftoppm.exe");
                if (File.Exists(p))
                {
                    _pdfRendererCache = ("pdftoppm", p);
                    _pdfRendererCacheInitialized = true;
                    return _pdfRendererCache;
                }
            }

            // 4. pymupdf (fitz) en el venv de oemer — buscamos python en venv
            var (oemerExe, _) = ResolveOemerCommand();
            if (!string.IsNullOrWhiteSpace(oemerExe))
            {
                var venvDir = Path.GetDirectoryName(oemerExe) ?? string.Empty; // …/Scripts
                var pythonExe = Path.Combine(venvDir, "python.exe");
                if (!File.Exists(pythonExe))
                    pythonExe = Path.Combine(venvDir, "..", "python.exe");
                if (File.Exists(pythonExe))
                {
                    // Comprobación rápida sin lanzar proceso (asumimos que si tiene oemer tiene fitz instalado
                    // si el package está presente en site-packages)
                    var sitePackages = Path.Combine(venvDir, "..", "Lib", "site-packages", "fitz");
                    if (Directory.Exists(sitePackages))
                    {
                        _pdfRendererCache = ("pymupdf", Path.GetFullPath(pythonExe));
                        _pdfRendererCacheInitialized = true;
                        return _pdfRendererCache;
                    }
                }
            }

            _pdfRendererCache = null;
            _pdfRendererCacheInitialized = true;
            return null;
        }
    }

    /// <summary>
    /// Convierte un PDF en imágenes PNG (una por página) en <paramref name="outDir"/>.
    /// Devuelve la lista de rutas PNG generadas en orden de página, o lista vacía si falla.
    /// </summary>
    private async Task<List<string>> ConvertPdfToPngPagesAsync(
        string pdfPath, string outDir, (string Kind, string Exe) renderer, int dpi, CancellationToken ct)
    {
        var pages = new List<string>();
        var pdfName = Path.GetFileNameWithoutExtension(pdfPath);

        if (renderer.Kind == "gs")
        {
            var outputPattern = Path.Combine(outDir, "page_%04d.png");
            var args = new[]
            {
                "-dBATCH", "-dNOPAUSE", "-dQUIET",
                "-sDEVICE=pnggray", $"-r{dpi}",
                $"-sOutputFile={outputPattern}",
                pdfPath
            };
            var ok = await RunProcessAsync(renderer.Exe, args, pdfName, "GS PDF→PNG", TimeSpan.FromMinutes(5), ct).ConfigureAwait(true);
            if (!ok) return pages;
        }
        else if (renderer.Kind == "pdftoppm")
        {
            var prefix = Path.Combine(outDir, "page");
            var args = new[] { "-r", dpi.ToString(), "-gray", "-png", pdfPath, prefix };
            var ok = await RunProcessAsync(renderer.Exe, args, pdfName, "pdftoppm PDF→PNG", TimeSpan.FromMinutes(5), ct).ConfigureAwait(true);
            if (!ok) return pages;
        }
        else if (renderer.Kind == "pymupdf")
        {
            var script =
                "import fitz,sys,os;" +
                "doc=fitz.open(sys.argv[1]);" +
                $"dpi={dpi};" +
                "[doc[i].get_pixmap(matrix=fitz.Matrix(dpi/72,dpi/72),colorspace=fitz.csGRAY)" +
                ".save(os.path.join(sys.argv[2],f'page_{{i+1:04d}}.png')) for i in range(len(doc))]";
            var args = new[] { "-c", script, pdfPath, outDir };
            var ok = await RunProcessAsync(renderer.Exe, args, pdfName, "pymupdf PDF→PNG", TimeSpan.FromMinutes(5), ct).ConfigureAwait(true);
            if (!ok) return pages;
        }

        try
        {
            pages.AddRange(
                Directory.EnumerateFiles(outDir, "page_*.png")
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        }
        catch { }

        return pages;
    }

    private int ComputeOemerTimeoutSeconds(string inputPath, bool isFromPdf = false, int pdfPageCount = 1, string? timeoutFamilyKey = null, long knownSizeBytes = -1)
    {
        var familyKey = string.IsNullOrWhiteSpace(timeoutFamilyKey)
            ? NormalizeAudiverisFamilyKey(inputPath)
            : timeoutFamilyKey;
        var strike = _oemerTimeoutStrikes.TryGetValue(familyKey, out var value) ? value : 0;
        var isPdfInput = isFromPdf || string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdfInput)
            return _oemerTimeoutSeconds + (strike * _oemerTimeoutStrikeBoostSeconds);

        var baseSeconds = Math.Max(_oemerTimeoutSeconds, _oemerPdfTimeoutMinSeconds);
        long bytes;
        if (knownSizeBytes >= 0)
        {
            bytes = knownSizeBytes;
        }
        else
        {
            try { bytes = new FileInfo(inputPath).Length; }
            catch { bytes = 0; }
        }
        var sizeMb = Math.Max(1, (int)Math.Ceiling(bytes / (1024d * 1024d)));
        var extraPages = Math.Max(0, pdfPageCount - 1);
        var adaptive = baseSeconds
                       + (sizeMb * _oemerPdfTimeoutPerMbSeconds)
                       + (extraPages * _oemerPdfTimeoutPerPageSeconds)
                       + (strike * _oemerTimeoutStrikeBoostSeconds);
        var clamped = Math.Clamp(adaptive, _oemerPdfTimeoutMinSeconds, _oemerPdfTimeoutMaxSeconds);
        if (isFromPdf)
            LogDebug($"🎵 oemer timeout ({Path.GetFileName(inputPath)}): base={baseSeconds}s size={sizeMb}MB pages={Math.Max(1, pdfPageCount)} strike={strike} => {clamped}s");
        return clamped;
    }

    private int GetEffectiveAudiverisParallel()
    {
        // Reservar al menos un core para UI/IO evita saturación en CPU-only.
        var cpuBoundCap = Math.Max(1, Environment.ProcessorCount - 1);
        return Math.Max(1, Math.Min(_audiverisParallel, cpuBoundCap));
    }

    private int GetEffectiveOemerParallel()
    {
        // Reservar al menos un core para UI/IO evita saturación en CPU-only.
        var cpuBoundCap = Math.Max(1, Environment.ProcessorCount - 1);
        return Math.Max(1, Math.Min(_oemerParallel, cpuBoundCap));
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
            foreach (var d in subdirs)
            {
                var dirName = Path.GetFileName(d);
                if (!dirName.Equals("_AudiverisFailed", StringComparison.OrdinalIgnoreCase) &&
                    !dirName.Equals("_OemerFailed", StringComparison.OrdinalIgnoreCase))
                    stack.Push(d);
            }
        }
    }

    /// <summary>
    /// Obtiene todos los archivos del directorio con caché de 60s para evitar re-escaneos en ciclos
    /// Audiveris → oemer → Video que comparten la misma carpeta destino.
    /// </summary>
    private List<string> GetRawFilesFromDir(string rootDir)
    {
        var now = DateTime.UtcNow;
        if (_rawFilesCache.TryGetValue(rootDir, out var cached) && now < cached.Time.Add(_rawFilesCacheTtl))
            return cached.Files;
        var files = SafeEnumerateFiles(rootDir, _ => true).ToList();
        _rawFilesCache[rootDir] = (now, files);
        return files;
    }

    private void InvalidateRawFilesCache(string rootDir)
    {
        if (string.IsNullOrWhiteSpace(rootDir))
            return;
        _rawFilesCache.Remove(rootDir);
        _siblingSetCache.Remove(rootDir);
    }

    /// <summary>
    /// Versión con caché de SafeEnumerateFiles. Evita re-escanear el árbol completo dentro del mismo ciclo.
    /// </summary>
    private IEnumerable<string> SafeEnumerateFilesCached(string rootDir, Func<string, bool> filter)
        => GetRawFilesFromDir(rootDir).Where(filter);

    /// <summary>
    /// Versión con caché de BuildMusicScoreSiblingSet. Reutiliza el escaneo raw de GetRawFilesFromDir
    /// y almacena el HashSet derivado con el mismo TTL.
    /// </summary>
    private HashSet<string> BuildMusicScoreSiblingSetCached(string rootDir)
    {
        var now = DateTime.UtcNow;
        if (_siblingSetCache.TryGetValue(rootDir, out var cachedSet) && now < cachedSet.Time.Add(_rawFilesCacheTtl))
            return cachedSet.Set;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in GetRawFilesFromDir(rootDir))
            {
                var ext = Path.GetExtension(f);
                if (!AudiverisOutputExtensions.Contains(ext)) continue;

                var dir = Path.GetDirectoryName(f) ?? string.Empty;
                var stem = Path.GetFileNameWithoutExtension(f);
                string canonDir;
                try { canonDir = Path.GetFullPath(dir); } catch { canonDir = dir; }

                set.Add(canonDir + "|" + stem);

                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName) &&
                    string.Equals(dirName, stem, StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(dir) ?? string.Empty;
                    string canonParent;
                    try { canonParent = Path.GetFullPath(parentDir); } catch { canonParent = parentDir; }
                    set.Add(canonParent + "|" + stem);
                }

                // oemer página: stem_p0001.mxl → base stem sin sufijo _p\d+
                var m = _pageSuffixRegex.Match(stem);
                if (m.Success)
                    set.Add(canonDir + "|" + m.Groups[1].Value);
            }
        }
        catch { }

        _siblingSetCache[rootDir] = (now, set);
        return set;
    }

    /// <summary>
    /// Mueve el PDF fallido a una subcarpeta <c>_AudiverisFailed</c> junto a él.
    /// Devuelve la nueva ruta si tuvo éxito, null si el archivo ya no existe o el movimiento falló.
    /// </summary>
    private static string? MoveToAudiverisFailedFolder(string inputPath)
    {
        try
        {
            if (!File.Exists(inputPath)) return null;
            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrEmpty(dir)) return null;
            // Evitar doble anidado si el archivo ya está dentro de _AudiverisFailed/
            if (Path.GetFileName(dir).Equals("_AudiverisFailed", StringComparison.OrdinalIgnoreCase))
                return inputPath;
            var failedDir = Path.Combine(dir, "_AudiverisFailed");
            Directory.CreateDirectory(failedDir);
            var dest = Path.Combine(failedDir, Path.GetFileName(inputPath));
            File.Move(inputPath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            return null; // best-effort; el archivo queda donde está
        }
    }

    private static bool HasMusicScoreSibling(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) return false;

        foreach (var ext in AudiverisOutputExtensions)
        {
            // Exact match: stem.mxl
            if (File.Exists(Path.Combine(dir, stem + ext))) return true;
            // Audiveris: stem/stem.mxl subdirectorio
            if (File.Exists(Path.Combine(dir, stem, stem + ext))) return true;
            // oemer página: stem_p0001.mxl (generado por ConvertPdfWithOemerAsync)
            try
            {
                if (Directory.EnumerateFiles(dir, stem + "_p*" + ext).Any()) return true;
            }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// Escanea rootDir una sola vez y construye un HashSet de claves "canonicalDir|stem"
    /// para todos los archivos de salida de Audiveris (.mxl/.xml/.mscz/.mscx).
    /// Usar con <see cref="HasMusicScoreSiblingFast"/> en operaciones sobre lotes grandes.
    /// </summary>
    private static HashSet<string> BuildMusicScoreSiblingSet(string rootDir)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(f);
                if (!AudiverisOutputExtensions.Contains(ext)) continue;

                var dir = Path.GetDirectoryName(f) ?? string.Empty;
                var stem = Path.GetFileNameWithoutExtension(f);
                string canonDir;
                try { canonDir = Path.GetFullPath(dir); } catch { canonDir = dir; }

                // Coincidencia directa: destFolder/.../stem.ext → "canonDir|stem"
                set.Add(canonDir + "|" + stem);

                // Coincidencia en subdirectorio: destFolder/.../stem/stem.ext →
                // la clave que usa HasMusicScoreSibling es el directorio padre
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName) &&
                    string.Equals(dirName, stem, StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(dir) ?? string.Empty;
                    string canonParent;
                    try { canonParent = Path.GetFullPath(parentDir); } catch { canonParent = parentDir; }
                    set.Add(canonParent + "|" + stem);
                }

                // oemer página: stem_p0001.mxl → base stem sin sufijo _p\d+
                // Así stem_p0001 → clave canonDir|stem (sin _p0001) para el PDF original
                var pageSuffix = _pageSuffixRegex.Match(stem);
                if (pageSuffix.Success)
                {
                    var baseStem = pageSuffix.Groups[1].Value;
                    set.Add(canonDir + "|" + baseStem);
                }
            }
        }
        catch { }
        return set;
    }

    /// <summary>O(1) variante de <see cref="HasMusicScoreSibling"/> usando un set preconstruido.</summary>
    private static bool HasMusicScoreSiblingFast(string inputPath, HashSet<string> siblingSet)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) return false;
        string canonDir;
        try { canonDir = Path.GetFullPath(dir); } catch { canonDir = dir; }
        return siblingSet.Contains(canonDir + "|" + stem);
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

    private static List<string> SelectFirstPerAudiverisFamily(IEnumerable<string> files, out int skippedDuplicates, out Dictionary<string, long> fileSizes)
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
        fileSizes = new Dictionary<string, long>(bestPerFamily.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in bestPerFamily.Values)
            fileSizes[kv.Path] = kv.Size;
        return bestPerFamily.Values
            .Select(v => v.Path)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ComputeAudiverisTimeoutSeconds(string inputPath, long knownSizeBytes = -1, int pdfPageCount = 1)
    {
        var familyKey = NormalizeAudiverisFamilyKey(inputPath);
        var strike = _audiverisTimeoutStrikes.TryGetValue(familyKey, out var value) ? value : 0;

        var isPdfInput = string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdfInput)
            return _audiverisTimeoutSeconds + (strike * _audiverisTimeoutStrikeBoostSeconds);

        var baseSeconds = Math.Max(_audiverisTimeoutSeconds, _audiverisPdfTimeoutMinSeconds);
        long bytes;
        if (knownSizeBytes >= 0)
            bytes = knownSizeBytes;
        else
        {
            try { bytes = new FileInfo(inputPath).Length; }
            catch { bytes = 0; }
        }

        var sizeMb = Math.Max(1, (int)Math.Ceiling(bytes / (1024d * 1024d)));
        var extraPages = Math.Max(0, pdfPageCount - 1);
        var adaptive = baseSeconds + (sizeMb * _audiverisPdfTimeoutPerMbSeconds) + (extraPages * _audiverisPdfTimeoutPerPageSeconds) + (strike * _audiverisTimeoutStrikeBoostSeconds);
        LogDebug($"🎼 audiveris timeout ({Path.GetFileName(inputPath)}): base={baseSeconds}s size={sizeMb}MB pages={Math.Max(1, pdfPageCount)} strike={strike} => {Math.Clamp(adaptive, _audiverisPdfTimeoutMinSeconds, _audiverisPdfTimeoutMaxSeconds)}s");
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
        lock (_audiverisExeCacheLock)
        {
            if (_audiverisExeCacheInitialized) return _audiverisExeCache;
            _audiverisExeCache = ResolveAudiverisExecutableCore();
            _audiverisExeCacheInitialized = true;
            return _audiverisExeCache;
        }
    }

    private static string? ResolveAudiverisExecutableCore()
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

    private async Task<(bool Success, bool Partial, bool PageFailure)> RunAudiverisConversionAsync(string audiverisExe, string inputPath, string fallbackOutputDir = "", bool allowSiblingFallback = true, HashSet<string>? siblingSet = null, long knownSizeBytes = -1)
    {
        var outputDir = Path.GetDirectoryName(inputPath) ?? (string.IsNullOrEmpty(fallbackOutputDir) ? string.Empty : fallbackOutputDir);
        bool HasSiblingFastOrSlow(string path)
            => siblingSet is not null ? HasMusicScoreSiblingFast(path, siblingSet) : HasMusicScoreSibling(path);
        static string Tail(string s) => s.Length > 500 ? "..." + s[^500..] : s;

        async Task<(int exitCode, string stdout, string stderr)> RunAudiverisOnceAsync(string currentInputPath, IEnumerable<string> extraArgs)
        {
            // Use pre-computed size when processing the original input path to avoid a second stat() call
            var sizeForTimeout = string.Equals(currentInputPath, inputPath, StringComparison.OrdinalIgnoreCase) ? knownSizeBytes : -1L;
            var timeoutSeconds = ComputeAudiverisTimeoutSeconds(currentInputPath, sizeForTimeout);
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
            if (!process.Start())
                throw new InvalidOperationException($"No se pudo iniciar Audiveris: {psi.FileName}");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            // Delay con CancellationToken para que no quede timer huérfano tras éxito
            using var delayCts = new CancellationTokenSource();
            var delayTask = Task.Delay(audiverisTimeout, delayCts.Token);
            var completed = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(true);
            if (completed != waitTask)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }

                try { await Task.WhenAll(stdoutTask, stderrTask, waitTask).ConfigureAwait(true); } catch { }
                throw new TimeoutException($"Audiveris timeout tras {audiverisTimeout.TotalMinutes:0.#} min ({timeoutSeconds}s)");
            }

            delayCts.Cancel(); // libera el timer del Delay antes de que expire
            await waitTask.ConfigureAwait(true);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(true);
            var stdout = stdoutTask.Result.Trim();
            var stderr = stderrTask.Result.Trim();
            LogDebug($"🎼 Audiveris exit ({Path.GetFileName(currentInputPath)}): code={process.ExitCode}");
            return (process.ExitCode, stdout, stderr);
        }

        static bool IsAudiverisPageFailure(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Contains("Error in reaching step PAGE", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("reaching step PAGE", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("step PAGE", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("error", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Contains("PAGE", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("StepException", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        (bool Success, bool Partial, bool PageFailure) BuildFailureResult((int exitCode, string stdout, string stderr) attempt, string attemptedPath)
        {
            var firstCombined = string.Join("\n", new[] { attempt.stderr, attempt.stdout }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var isPageFailure = IsAudiverisPageFailure(firstCombined);
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

        bool CanFallbackToSibling(string originalInputPath, out string siblingPath)
        {
            siblingPath = string.Empty;
            if (!TryGetAudiverisSiblingVariant(originalInputPath, out var candidateSiblingPath))
                return false;
            if (HasSiblingFastOrSlow(candidateSiblingPath))
                return false;
            var siblingFamilyKey = NormalizeAudiverisFamilyKey(candidateSiblingPath);
            if (IsAudiverisFamilyInTimeoutCooldown(candidateSiblingPath, siblingFamilyKey))
                return false;
            siblingPath = candidateSiblingPath;
            return true;
        }

        try
        {
            var firstTry = await RunAudiverisOnceAsync(inputPath, Array.Empty<string>()).ConfigureAwait(true);
            if (firstTry.exitCode == 0)
                return (HasMusicScoreSibling(inputPath), false, false);

            var firstFail = BuildFailureResult(firstTry, inputPath);
            if (!allowSiblingFallback || firstFail.PageFailure)
                return firstFail;

            if (!CanFallbackToSibling(inputPath, out var siblingPath))
                return firstFail;

            Log($"ℹ️ Audiveris fallback: {Path.GetFileName(inputPath)} fallo; reintento con variante hermana {Path.GetFileName(siblingPath)}.");
            var siblingTry = await RunAudiverisOnceAsync(siblingPath, Array.Empty<string>()).ConfigureAwait(true);
            if (siblingTry.exitCode == 0)
                return (true, false, false);
            return BuildFailureResult(siblingTry, siblingPath);
        }
        catch (TimeoutException) when (allowSiblingFallback && CanFallbackToSibling(inputPath, out var siblingPath))
        {
            Log($"ℹ️ Audiveris fallback por timeout: {Path.GetFileName(inputPath)}; reintento con variante hermana {Path.GetFileName(siblingPath)}.");
            var siblingTry = await RunAudiverisOnceAsync(siblingPath, Array.Empty<string>()).ConfigureAwait(true);
            if (siblingTry.exitCode == 0)
                return (true, false, false);
            return BuildFailureResult(siblingTry, siblingPath);
        }
    }

    // ── Video generation (MuseScore directo) ───────────────────────────────────

    private static readonly HashSet<string> VideoInputExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mscz", ".mxl", ".xml", ".musicxml" };

    private volatile bool _videoRunning;
    private CancellationTokenSource? _videoCts;
    private readonly int _videoTimeoutSeconds = ReadFeatureFlagInt("SCOREDOWN_VIDEO_TIMEOUT_SEC", 1200, 30, 7200);
    private readonly int _videoParallel = ReadFeatureFlagInt("SCOREDOWN_VIDEO_PARALLEL", 1, 1, 4);
    private readonly int _videoTrimTailSeconds = ReadFeatureFlagInt("SCOREDOWN_VIDEO_TRIM_TAIL_SEC", 6, 0, 30);
    private readonly int _videoSubtitleFontPt = ReadFeatureFlagInt("SCOREDOWN_VIDEO_SUBTITLE_FONT_PT", 11, 6, 36);
    private readonly bool _videoLuxuryTitleEnabled = ReadFeatureFlag("SCOREDOWN_VIDEO_LUXURY_TITLE", true);
    private readonly int _videoLuxuryTitleSeconds = ReadFeatureFlagInt("SCOREDOWN_VIDEO_LUXURY_DURATION_SEC", 5, 1, 30);
    private readonly bool _videoHideOriginalScoreTitle = ReadFeatureFlag("SCOREDOWN_VIDEO_HIDE_ORIGINAL_TITLE", true);
    private readonly int _videoFadeOutSeconds = ReadFeatureFlagInt("SCOREDOWN_VIDEO_FADE_OUT_SEC", 2, 0, 10);

    private readonly System.Collections.ObjectModel.ObservableCollection<VideoSelectItem> _videoSelectItems = new();
    private string? _videoPanelMuseScoreExe;
    private IReadOnlyList<string>? _videoPanelExtraArgs;
    private string? _videoPanelDestFolder;

    // Panel de vídeo: filtro, ordenación y ETA
    private System.ComponentModel.ICollectionView? _videoSelectView;
    private string _videoFilterText = string.Empty;
    private bool _videoIncludeRegenerate;
    private string _videoSortColumn = string.Empty;
    private bool _videoSortAscending = true;
    private bool _videoRefreshing;
    private bool _suppressVideoHeaderEvents;
    private bool _suppressItemCheckEvents;   // evita O(N²) UpdateVideoSelectButton en operaciones batch
    private VideoSelectItem? _ctxMenuTargetItem;
    private int _videoTotalSelected;          // contador O(1) de items con IsSelected=true
    private CancellationTokenSource? _videoPopulateCts;
    private System.Windows.Controls.GridViewColumnHeader? _lastSortHeader;
    private object? _lastSortOriginalContent;

    // ETA y progreso durante generación
    private int _videoEtaTotal;
    private int _videoEtaCompleted;
    private int _videoEtaFailed;       // contador de fallos para el mensaje ETA final
    private DateTimeOffset _videoEtaStart;

    private async void BtnGenerateVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_audiverisRunning) { Log("⚠️ Hay una conversión Audiveris en proceso. Espera a que termine."); return; }
        if (_videoRunning) { Log("⚠️ Ya hay una generación de vídeo en curso."); return; }

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

        _videoPanelMuseScoreExe = museScoreExe;
        _videoPanelExtraArgs = ResolveMuseScoreVideoArgs();
        _videoPanelDestFolder = destFolder;

        // Cancelar populate anterior si el panel ya estaba abierto con otra carpeta
        _videoPopulateCts?.Cancel();
        _videoPopulateCts?.Dispose();
        _videoPopulateCts = null;

        try
        {
            await PopulateVideoSelectPanelAsync(destFolder).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log($"❌ Error al cargar partituras para vídeo: {ex.Message}");
            return;
        }
        pnlVideoSelect.Visibility = System.Windows.Visibility.Visible;
    }

    private async Task PopulateVideoSelectPanelAsync(string destFolder)
    {
        if (string.IsNullOrWhiteSpace(destFolder)) return;   // guardia de seguridad
        // Guardar sort activo para restaurarlo tras el populate
        var savedSortColumn = _videoSortColumn;
        var savedSortAscending = _videoSortAscending;

        // Resetear indicador visual de ordenación (el header del populate nuevo empieza sin flechas)
        if (_lastSortHeader is not null && _lastSortOriginalContent is not null)
            _lastSortHeader.Content = _lastSortOriginalContent;
        _lastSortHeader = null;
        _lastSortOriginalContent = null;
        _videoSortColumn = string.Empty;
        _videoSortAscending = true;
        _videoSelectView?.SortDescriptions.Clear();  // evita sort "fantasma" tras refresh

        // Cancelar populate anterior si aún corría
        _videoPopulateCts?.Cancel();
        _videoPopulateCts?.Dispose();
        _videoPopulateCts = new CancellationTokenSource();
        var localCts = _videoPopulateCts;

        txtStatus.Text = "🔍 Escaneando partituras...";

        // Capturar selección previa en hilo UI (ObservableCollection no es thread-safe)
        bool isFirstLoad = _videoSelectItems.Count == 0;
        var previouslySelected = isFirstLoad
            ? null
            : new HashSet<string>(
                _videoSelectItems.Where(i => i.IsSelected).Select(i => i.BestFile),
                StringComparer.OrdinalIgnoreCase);

        List<(string File, string Ext, string Tag, bool HasVideo, string FolderShort, string Mp4ToolTip)> computed;
        try
        {
            computed = await Task.Run(() =>
            {
                var allInputs = SafeEnumerateFilesCached(destFolder,
                    f => VideoInputExtensions.Contains(Path.GetExtension(f))).ToList();

                // Precompute MP4 set del caché existente → evita File.Exists por archivo
                var mp4Set = new HashSet<string>(
                    GetRawFilesFromDir(destFolder)
                        .Where(f => Path.GetExtension(f).Equals(".mp4", StringComparison.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);
                bool hasMp4(string f) => VideoSiblingCandidates(f).Any(c => mp4Set.Contains(c));

                return allInputs
                    .GroupBy(f => Path.Combine(
                        Path.GetDirectoryName(f) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(f)),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderBy(f =>
                    {
                        var idx = Array.IndexOf(VideoFormatPriority, Path.GetExtension(f).ToLowerInvariant());
                        return idx < 0 ? 999 : idx;
                    }).First())
                    .Select(file =>
                    {
                        localCts.Token.ThrowIfCancellationRequested();
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        var hv = hasMp4(file);   // HashSet lookup, sin IO adicional
                        var dir = Path.GetDirectoryName(file) ?? string.Empty;
                        // Ruta relativa al destFolder para FolderShort (más informativo que truncado de cola)
                        string folderShort;
                        if (dir.StartsWith(destFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = dir.Substring(destFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            folderShort = string.IsNullOrEmpty(rel) ? "(raíz)"
                                : rel.Length > 45 ? "…" + rel[^44..] : rel;
                        }
                        else
                        {
                            folderShort = dir.Length > 45 ? "…" + dir[^44..] : dir;
                        }
                        return (
                            File: file,
                            Ext: ext,
                            Tag: ext.TrimStart('.').ToUpperInvariant(),
                            HasVideo: hv,
                            FolderShort: folderShort,
                            Mp4ToolTip: hv ? ComputeMp4SizeToolTip(file) : string.Empty
                        );
                    })
                    .OrderBy(d => d.FolderShort, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => !d.HasVideo)
                    .ThenBy(d => Path.GetFileNameWithoutExtension(d.File), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { return; }  // populate más nuevo en camino, ignorar silencioso

        // Actualizar UI en hilo principal
        if (localCts.IsCancellationRequested) return;   // populate más nuevo en camino
        btnVideoSelectGenerate.IsEnabled = false;        // bloquear hasta que UpdateVideoSelectButton fije el estado correcto
        _videoTotalSelected = 0;
        _suppressItemCheckEvents = true;    // evitar O(N²) UpdateVideoSelectButton durante el llenado
        _videoSelectItems.Clear();
        foreach (var d in computed)
        {
            System.Windows.Media.Brush color = d.Ext switch
            {
                ".mscz" => BrushMscz,
                ".mxl" => BrushMxl,
                ".musicxml" => BrushMusicXml,
                ".xml" => BrushMusicXml,   // MusicXML con extensión .xml
                _ => System.Windows.Media.Brushes.DimGray
            };
            var isSelected = previouslySelected is null ? !d.HasVideo : previouslySelected.Contains(d.File);
            var item = new VideoSelectItem
            {
                BestFile = d.File,
                DisplayName = Path.GetFileNameWithoutExtension(d.File),
                FormatTag = d.Tag,
                FormatColor = color,
                HasVideo = d.HasVideo,
                FolderShort = d.FolderShort,
                Mp4SizeToolTip = d.Mp4ToolTip,
            };
            item.SelectionDelta = delta => _videoTotalSelected += delta;
            item.IsSelected = isSelected;   // dispara delta → _videoTotalSelected
            _videoSelectItems.Add(item);
        }
        _suppressItemCheckEvents = false;   // reactivar después del llenado

        // Agrupar solo cuando hay más de una carpeta distinta (un solo grupo sería ruido visual)
        var hasMultiFolders = _videoSelectItems
            .Select(i => i.FolderShort)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1).Any();   // más eficiente que Count() > 1

        // Crear o refrescar CollectionView (mantiene filtro/orden activos)
        if (_videoSelectView is null || !ReferenceEquals(_videoSelectView.SourceCollection, _videoSelectItems))
        {
            _videoSelectView = System.Windows.Data.CollectionViewSource.GetDefaultView(_videoSelectItems);
            _videoSelectView.Filter = FilterVideoItem;
        }
        // Sincronizar grouping con la realidad de carpetas del lote actual
        _videoSelectView.GroupDescriptions.Clear();
        if (hasMultiFolders)
            _videoSelectView.GroupDescriptions.Add(
                new System.Windows.Data.PropertyGroupDescription(nameof(VideoSelectItem.FolderShort)));
        // Ocultar columna "Carpeta" cuando hay cabeceras de grupo (información redundante)
        if (colCarpeta is not null) colCarpeta.Width = hasMultiFolders ? 0 : 220;

        // Restaurar sort activo (si había uno antes del populate)
        if (!string.IsNullOrEmpty(savedSortColumn) && _videoSelectView is not null)
        {
            _videoSortColumn = savedSortColumn;
            _videoSortAscending = savedSortAscending;
            if (_videoSelectView.GroupDescriptions.Count > 0 && savedSortColumn != nameof(VideoSelectItem.FolderShort))
                _videoSelectView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                    nameof(VideoSelectItem.FolderShort), System.ComponentModel.ListSortDirection.Ascending));
            _videoSelectView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                savedSortColumn, savedSortAscending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending));
        }

        _videoSelectView!.Refresh();
        lstVideoSelect.ItemsSource = _videoSelectView;

        txtVideoEta.Text = string.Empty;
        pbVideoGeneral.Value = 0;
        pbVideoGeneral.Foreground = System.Windows.Media.Brushes.MediumPurple;   // reset tras posibles errores

        // Dispose del CTS ahora que populate terminó normalmente
        if (!localCts.IsCancellationRequested)
        {
            _videoPopulateCts = null;
            localCts.Dispose();
        }

        // Actualizar status tras populate exitoso
        var pending = _videoSelectItems.Count(i => !i.HasVideo);
        txtStatus.Text = $"🎥 {_videoSelectItems.Count} partitura(s) cargadas, {pending} sin vídeo";

        // Scroll al inicio para que el usuario vea el comienzo de la lista
        var firstVisible = VisibleItems.FirstOrDefault();
        if (firstVisible is not null) lstVideoSelect.ScrollIntoView(firstVisible);

        UpdateVideoSelectButton();
        // Actualizar color del filtro: puede haber cambiado número de coincidencias tras el nuevo escaneo
        TxtVideoFilter_TextChanged(txtVideoFilter, null!);
    }

    private static readonly System.Windows.Media.SolidColorBrush BrushMscz = MakeFrozenBrush(0x7C, 0x3A, 0xED);
    private static readonly System.Windows.Media.SolidColorBrush BrushMxl = MakeFrozenBrush(0x0F, 0x76, 0x6E);
    private static readonly System.Windows.Media.SolidColorBrush BrushMusicXml = MakeFrozenBrush(0x1E, 0x40, 0xAF);

    private static System.Windows.Media.SolidColorBrush MakeFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private bool FilterVideoItem(object obj)
    {
        if (obj is not VideoSelectItem item) return false;
        // Errores y ítems en curso siempre visibles, independientemente de filtros
        if (item.Status == VideoStatus.Error || item.Status == VideoStatus.Running) return true;
        // Filtrar por texto: nombre O carpeta O formato
        if (!string.IsNullOrEmpty(_videoFilterText)
            && !item.DisplayName.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase)
            && !item.FolderShort.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase)
            && !item.FormatTag.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase))
            return false;
        // Si no se quiere regenerar, ocultar los que ya tienen vídeo
        if (!_videoIncludeRegenerate && item.HasVideo && item.Status == VideoStatus.None)
            return false;
        return true;
    }

    private void UpdateVideoSelectButton()
    {
        // Un solo bucle sobre _videoSelectItems para todos los contadores O(N)
        int allErrors = 0, allPending = 0;
        foreach (var i in _videoSelectItems)
        {
            if (i.Status == VideoStatus.Error) allErrors++;
            if (!i.HasVideo && i.Status != VideoStatus.Running) allPending++;
        }

        // Un solo bucle sobre VisibleItems para sel, total y visiblePending
        int sel = 0, total = 0, visiblePending = 0;
        foreach (var i in VisibleItems)
        {
            total++;
            if (i.IsSelected) sel++;
            if (!i.HasVideo && i.Status == VideoStatus.None) visiblePending++;
        }

        // hiddenSel: seleccionados ocultos por el filtro (O(1) via _videoTotalSelected)
        var hiddenSel = _videoTotalSelected - sel;
        var totalSel = _videoTotalSelected;
        btnVideoSelectGenerate.Content = totalSel > sel
            ? $"🎥 Generar ({totalSel})"  // muestra total real incluyendo ocultos
            : $"🎥 Generar ({sel})";
        btnVideoSelectGenerate.IsEnabled = totalSel > 0 && !_videoRunning;
        if (btnVideoSelectErrors is not null)
            btnVideoSelectErrors.IsEnabled = allErrors > 0 && !_videoRunning;
        // Deshabilitar botones de selección masiva durante generación para evitar cambios involuntarios
        var selectionEnabled = !_videoRunning;
        if (btnVideoSelectAll is not null) btnVideoSelectAll.IsEnabled = selectionEnabled;
        if (btnVideoSelectNone is not null) btnVideoSelectNone.IsEnabled = selectionEnabled;
        if (btnVideoSelectInvert is not null) btnVideoSelectInvert.IsEnabled = selectionEnabled;
        if (btnVideoSelectPending is not null) btnVideoSelectPending.IsEnabled = selectionEnabled;
        if (btnVideoSelectRefresh is not null) btnVideoSelectRefresh.IsEnabled = selectionEnabled && !_videoRefreshing;
        string countText = allErrors > 0
            ? $"{sel}/{total} sel  •  {allErrors}⚠ error(es)"
            : $"{sel}/{total} seleccionada(s)";
        if (hiddenSel > 0) countText += $"  +{hiddenSel} oculta(s) sel.";
        txtVideoSelectCount.Text = countText;

        // Sync checkbox de cabecera (sin disparar eventos)
        _suppressVideoHeaderEvents = true;
        chkVideoSelectHeader.IsChecked = sel == 0 ? false : sel == total ? true : (bool?)null;
        _suppressVideoHeaderEvents = false;

        // Mensaje vacío cuando la vista filtrada no tiene ítems
        if (txtVideoEmpty is not null)
        {
            if (_videoSelectItems.Count == 0)
            {
                txtVideoEmpty.Text = "No se encontraron partituras (.mscz / .mxl / .musicxml) en la carpeta seleccionada";
                txtVideoEmpty.Visibility = System.Windows.Visibility.Visible;
            }
            else if (total == 0)
            {
                string emptyMsg;
                bool hasFilterText = !string.IsNullOrEmpty(_videoFilterText);
                // Detectar si Re-generar oculta ítems que coinciden con el filtro
                bool filterMatchesHiddenVideo = hasFilterText && !_videoIncludeRegenerate
                    && _videoSelectItems.Any(i => i.HasVideo && i.Status == VideoStatus.None
                        && (i.DisplayName.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase)
                         || i.FolderShort.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase)
                         || i.FormatTag.Contains(_videoFilterText, StringComparison.OrdinalIgnoreCase)));
                if (hasFilterText && filterMatchesHiddenVideo)
                {
                    emptyMsg = hiddenSel > 0
                        ? $"Resultados ocultos por Re-generar  •  Activa 🔁 para verlos  •  {hiddenSel} sel. oculta(s)"
                        : "Resultados ocultos por Re-generar. Activa 🔁 Re-generar para verlos";
                }
                else if (hasFilterText)
                {
                    emptyMsg = hiddenSel > 0
                        ? $"Sin coincidencias para el filtro actual  •  {hiddenSel} seleccionada(s) oculta(s)"
                        : "Sin coincidencias para el filtro actual";
                }
                else
                {
                    emptyMsg = hiddenSel > 0
                        ? $"Todas ya tienen vídeo MP4. Activa 🔁 Re-generar para mostrarlas  •  {hiddenSel} seleccionada(s) oculta(s)"
                        : "Todas las partituras ya tienen vídeo MP4. Activa 🔁 Re-generar para mostrarlas";
                }
                txtVideoEmpty.Text = emptyMsg;
                txtVideoEmpty.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                txtVideoEmpty.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        // Hint: refleja recuento filtrado vs total
        if (_videoPanelDestFolder is not null)
        {
            var allTotal = _videoSelectItems.Count;
            // Nombre corto de carpeta (solo último segmento, más manejable en la barra de hint)
            var folderName = Path.GetFileName(_videoPanelDestFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(folderName)) folderName = _videoPanelDestFolder;  // raíz de disco
            // Cuando hay filtro activo, mostrar pendientes visibles; si no, pendientes globales
            var pendingLabel = total < allTotal
                ? $"{visiblePending} sin vídeo (filtrado)"
                : $"{allPending} sin vídeo";
            var hintBase = total < allTotal
                ? $"📂 {folderName}   |   {total}/{allTotal} visible(s), {pendingLabel}"
                : $"📂 {folderName}   |   {allTotal} partitura(s), {pendingLabel}";
            txtVideoSelectHint.Text = allErrors > 0 ? $"{hintBase}  •  {allErrors}⚠️" : hintBase;
            txtVideoSelectHint.ToolTip = _videoPanelDestFolder;
        }
    }

    // Vista filtrada o colección completa si la vista aún no existe
    private IEnumerable<VideoSelectItem> VisibleItems =>
        _videoSelectView?.Cast<VideoSelectItem>() ?? _videoSelectItems;

    private async Task RunVideoGenerationAsync(IReadOnlyList<string> pending, string museScoreExe, IReadOnlyList<string> extraVideoArgs, string destFolder, CancellationToken externalCt)
    {
        // Guardia temprana: no debería ocurrir, pero mejor salir limpio antes de tocar UI
        if (pending.Count == 0) { Log("⚠️ Video: sin partituras que generar."); return; }

        var soundProfile = GetArgValue(extraVideoArgs, "--sound-profile") ?? "MuseSounds";
        Log($"🎥 Video: inicio para {pending.Count} partitura(s). Perfil audio: {soundProfile}");
        VideoLog($"🎥 Iniciando generación de {pending.Count} partitura(s)...");
        VideoLog($"Perfil de audio: {soundProfile}");

        _videoRunning = true;
        _videoCts = externalCt.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalCt)
            : new CancellationTokenSource();
        btnGenerateVideo.IsEnabled = false;
        btnVideoSelectGenerate.IsEnabled = false;
        btnVideoSelectCancelGen.IsEnabled = true;
        btnCancelVideo.IsEnabled = true;
        txtVideoFilter.IsEnabled = false;
        chkVideoRegenerate.IsEnabled = false;

        int ok = 0, fail = 0, processed = 0;

        // Lookup rápido para actualizar badges de estado en vivo (solo los ítems pendientes)
        var pendingSet = new HashSet<string>(pending, StringComparer.OrdinalIgnoreCase);
        var itemLookup = _videoSelectItems
            .Where(i => pendingSet.Contains(i.BestFile))
            .ToDictionary(i => i.BestFile, i => i, StringComparer.OrdinalIgnoreCase);

        // Inicializar progreso + ETA
        _videoEtaTotal = pending.Count;
        _videoEtaCompleted = 0;
        _videoEtaFailed = 0;
        _videoEtaStart = DateTimeOffset.UtcNow;
        await Dispatcher.InvokeAsync(() =>
        {
            pbVideoGeneral.Maximum = pending.Count;
            pbVideoGeneral.Value = 0;
            pbVideoGeneral.Foreground = System.Windows.Media.Brushes.MediumPurple;
            txtVideoEta.Text = string.Empty;
        });

        try
        {
            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions { MaxDegreeOfParallelism = _videoParallel, CancellationToken = _videoCts.Token },
                async (input, innerCt) =>
                {
                    var name = Path.GetFileName(input);
                    var idx = Interlocked.Increment(ref processed);
                    var outputMp4 = Path.Combine(
                        Path.GetDirectoryName(input) ?? destFolder,
                        Path.GetFileNameWithoutExtension(input) + ".mp4");
                    var startedAt = DateTimeOffset.UtcNow;

                    await Dispatcher.InvokeAsync(() =>
                        txtStatus.Text = $"🎥 Video [{idx}/{pending.Count}] {name}");
                    Log($"🎬 Video [{idx}/{pending.Count}] inicio: {name} -> {Path.GetFileName(outputMp4)}");
                    VideoLog($"[{idx}/{pending.Count}] Generando: {name}");

                    // Badge: en curso
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (itemLookup.TryGetValue(input, out var si))
                            si.Status = VideoStatus.Running;
                    });

                    bool generated;
                    try
                    {
                        generated = await RunMuseScoreVideoAsync(museScoreExe, input, outputMp4, extraVideoArgs, innerCt).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref fail);
                        Interlocked.Increment(ref _videoEtaFailed);
                        Log($"❌ Video error en {name}: {ex.Message}");
                        VideoLog($"❌ Error en {name}: {ex.Message}");
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (itemLookup.TryGetValue(input, out var si))
                                si.Status = VideoStatus.Error;
                            pbVideoGeneral.Foreground = System.Windows.Media.Brushes.OrangeRed;
                            pbVideoGeneral.Value = Interlocked.Increment(ref _videoEtaCompleted);
                            UpdateVideoEtaLabel();
                            UpdateVideoSelectButton();   // actualizar recuento de errores en vivo
                        });
                        return;
                    }

                    var elapsed = DateTimeOffset.UtcNow - startedAt;
                    if (generated)
                    {
                        if (_videoLuxuryTitleEnabled && File.Exists(outputMp4))
                        {
                            var decorated = await TryApplyLuxuryTitleOverlayAsync(outputMp4, input, innerCt).ConfigureAwait(false);
                            if (decorated)
                            {
                                Log($"✨ Intro portada aplicada ({name}): título + autor(es)");
                                VideoLog($"  ✨ Portada añadida");
                            }
                        }

                        if (_videoTrimTailSeconds > 0 && File.Exists(outputMp4))
                        {
                            var trimmed = await TryTrimVideoTailAsync(outputMp4, _videoTrimTailSeconds, innerCt).ConfigureAwait(false);
                            if (trimmed)
                            {
                                Log($"✂️ Recorte aplicado ({name}): -{_videoTrimTailSeconds}s al final");
                                VideoLog($"  ✂️ Recorte: -{_videoTrimTailSeconds}s");
                            }
                        }

                        if (_videoFadeOutSeconds > 0 && File.Exists(outputMp4))
                        {
                            var fadedOut = await TryApplyFadeOutAsync(outputMp4, _videoFadeOutSeconds, innerCt).ConfigureAwait(false);
                            if (fadedOut)
                            {
                                Log($"🌑 Fundido a negro aplicado ({name}): {_videoFadeOutSeconds}s al final");
                                VideoLog($"  🌑 Fundido: {_videoFadeOutSeconds}s");
                            }
                        }

                        var okNow = Interlocked.Increment(ref ok);
                        long sizeBytes = 0;
                        try { sizeBytes = new FileInfo(outputMp4).Length; } catch { }
                        var mb = sizeBytes > 0 ? $", {sizeBytes / (1024.0 * 1024.0):F1} MB" : string.Empty;
                        Log($"✅ Video OK: {name} en {elapsed.TotalSeconds:F1}s{mb} (ok={okNow}, fail={Volatile.Read(ref fail)})");
                        VideoLog($"✅ Completado: {elapsed.TotalSeconds:F1}s{mb}");

                        // Calcular tooltip de tamaño en hilo worker (evita FileInfo en UI thread)
                        var mp4ToolTip = ComputeMp4SizeToolTip(input);

                        // Refresh badge + HasVideo + ETA
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (itemLookup.TryGetValue(input, out var si))
                            {
                                si.HasVideo = true;
                                si.IsSelected = false;
                                si.Status = VideoStatus.Done;
                                si.Mp4SizeToolTip = mp4ToolTip;
                            }
                            pbVideoGeneral.Value = Interlocked.Increment(ref _videoEtaCompleted);
                            UpdateVideoEtaLabel();
                            UpdateVideoSelectButton();
                        });
                    }
                    else
                    {
                        var failNow = Interlocked.Increment(ref fail);
                        Interlocked.Increment(ref _videoEtaFailed);
                        Log($"⚠️ Sin vídeo generado: {name} tras {elapsed.TotalSeconds:F1}s (ok={Volatile.Read(ref ok)}, fail={failNow})");
                        VideoLog($"⚠️ Sin vídeo: {name}");
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (itemLookup.TryGetValue(input, out var si))
                                si.Status = VideoStatus.Error;
                            pbVideoGeneral.Foreground = System.Windows.Media.Brushes.OrangeRed;
                            pbVideoGeneral.Value = Interlocked.Increment(ref _videoEtaCompleted);
                            UpdateVideoEtaLabel();
                            UpdateVideoSelectButton();   // actualizar recuento de errores en vivo
                        });
                    }
                }).ConfigureAwait(true);

            var msg = fail == 0
                ? $"🎥 Vídeos completados: {ok} OK"
                : $"🎥 Vídeos completados: {ok} OK, {fail} sin generar";
            txtStatus.Text = msg;
            Log(msg);
            VideoLog($"\n📊 Resumen: {ok} OK · {fail} error");
        }
        catch (OperationCanceledException)
        {
            var msg = $"⏹️ Vídeo cancelado por usuario (ok={ok}, fail={fail})";
            txtStatus.Text = msg;
            Log(msg);
            VideoLog($"\n⏹️ Generación cancelada por usuario");
            var elapsedSec = (DateTimeOffset.UtcNow - _videoEtaStart).TotalSeconds;
            var elapsedStr = elapsedSec >= 60
                ? $"{(int)(elapsedSec / 60)}m {(int)(elapsedSec % 60):D2}s"
                : $"{(int)elapsedSec}s";
            txtVideoEta.Text = fail > 0
                ? $"⏹️ Cancelado ({ok}/{_videoEtaTotal}, ⚠{fail})  {elapsedStr}"
                : $"⏹️ Cancelado ({ok}/{_videoEtaTotal})  {elapsedStr}";
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error en generación de vídeo";
            Log($"❌ Video error: {ex.Message}");
            DarkDialogService.ShowMessage(this, $"Error generando vídeos: {ex.Message}", "Vídeo", MessageBoxButton.OK, MessageBoxImage.Error);
            txtVideoEta.Text = "❌ Error";
        }
        finally
        {
            _videoRunning = false;
            _videoCts?.Dispose();
            _videoCts = null;
            btnGenerateVideo.IsEnabled = true;
            btnCancelVideo.IsEnabled = false;
            btnVideoSelectCancelGen.IsEnabled = false;
            txtVideoFilter.IsEnabled = true;
            chkVideoRegenerate.IsEnabled = true;
            // Invalidar caché para que un Refresh posterior detecte los MP4 nuevos/borrados
            if (!string.IsNullOrEmpty(_videoPanelDestFolder))
                InvalidateRawFilesCache(_videoPanelDestFolder);
            // Mantener barra de progreso en el valor final (no resetear a 0)
            // Se reseteará automáticamente al iniciar la siguiente generación.
            // Resetear cualquier ítem que quedó en curso (cancelado o error fatal)
            foreach (var item in _videoSelectItems)
                if (item.Status == VideoStatus.Running) item.Status = VideoStatus.None;
            // Refrescar vista sin reconstruir (badges ya actualizados en tiempo real)
            _videoSelectView?.Refresh();
            UpdateVideoSelectButton();
            // Scroll al primer error para que el usuario lo vea
            var firstError = _videoSelectItems.FirstOrDefault(i => i.Status == VideoStatus.Error);
            if (firstError is not null)
                lstVideoSelect.ScrollIntoView(firstError);
        }
    }

    // Calcula y muestra el ETA restante. Llamar desde el hilo de UI.
    private void UpdateVideoEtaLabel()
    {
        var completed = Volatile.Read(ref _videoEtaCompleted);
        var failed = Volatile.Read(ref _videoEtaFailed);
        var total = _videoEtaTotal;
        if (completed <= 0 || total <= 0) return;   // no borrar ETA visible (ej. "⏹️ Cancelado")
        var elapsed = (DateTimeOffset.UtcNow - _videoEtaStart).TotalSeconds;
        if (elapsed < 0.1) return;                  // evitar división por cero / estimación no fiable
        var avgSec = elapsed / completed;
        var remaining = Math.Max(0, total - completed);
        if (remaining == 0)
        {
            var totalStr = elapsed >= 3600
                ? $"{(int)(elapsed / 3600)}h {(int)(elapsed % 3600 / 60):D2}m {(int)(elapsed % 60):D2}s"
                : elapsed >= 60
                    ? $"{(int)(elapsed / 60)}m {(int)(elapsed % 60):D2}s"
                    : $"{(int)elapsed}s";
            var doneText = failed > 0
                ? $"✅ Listo ({completed - failed} ok, ⚠{failed} error)  {totalStr}  (~{avgSec:F0}s/partitura)"
                : $"✅ Listo ({completed})  {totalStr}  (~{avgSec:F0}s/partitura)";
            txtVideoEta.Text = doneText;
            return;
        }
        var etaSec = avgSec * remaining;
        var ts = TimeSpan.FromSeconds(etaSec);
        var etaStr = ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s"
            : ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s"
                : $"{ts.Seconds}s";
        var failStr = failed > 0 ? $"  •  {failed}⚠" : string.Empty;
        var pct = total > 0 ? (int)Math.Round(completed * 100.0 / total) : 0;
        txtVideoEta.Text = $"⏱ {completed}/{total} ({pct}%)  ETA {etaStr}  (~{avgSec:F0}s/partitura){failStr}";
    }

    private void BtnCancelVideo_Click(object sender, RoutedEventArgs e)
    {
        _videoCts?.Cancel();
        if (btnCancelVideo != null) btnCancelVideo.IsEnabled = false;
        if (btnVideoSelectCancelGen != null) btnVideoSelectCancelGen.IsEnabled = false;
        Log("⏹️ Cancelando generación de vídeo...");
    }

    // ── Panel selección de vídeo: handlers ─────────────────────────────────────

    private void BtnVideoSelectBack_Click(object sender, RoutedEventArgs e)
    {
        if (_videoRunning)
        {
            DarkDialogService.ShowMessage(this,
                "Hay una generación de vídeo en curso. Para antes de volver.",
                "Vídeo en proceso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Cancelar populate si aún está en curso
        _videoPopulateCts?.Cancel();
        _videoPopulateCts?.Dispose();
        _videoPopulateCts = null;
        // Limpiar filtro para que el próximo populate arranque sin restricciones
        _videoFilterText = string.Empty;
        txtVideoFilter.Clear();
        // Liberar memoria: la colección puede ser grande (miles de archivos)
        _videoSelectItems.Clear();
        _videoTotalSelected = 0;
        _videoSelectView = null;
        lstVideoSelect.ItemsSource = null;
        txtVideoEta.Text = string.Empty;   // limpiar ETA de la última ejecución
        pbVideoGeneral.Value = 0;              // resetear barra al cerrar el panel
        txtStatus.Text = "Listo";  // restaurar status genérico tras cerrar panel
        pnlVideoSelect.Visibility = System.Windows.Visibility.Collapsed;
    }

    private void BtnVideoSelectAll_Click(object sender, RoutedEventArgs e)
    {
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = true;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void BtnVideoSelectNone_Click(object sender, RoutedEventArgs e)
    {
        _suppressItemCheckEvents = true;
        foreach (var item in _videoSelectItems) item.IsSelected = false;  // todos, no solo visibles
        _suppressItemCheckEvents = false;
        _videoTotalSelected = 0;    // reset directo: más seguro que depender de deltas
        UpdateVideoSelectButton();
    }

    private void BtnVideoSelectInvert_Click(object sender, RoutedEventArgs e)
    {
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = !item.IsSelected;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void BtnVideoSelectPending_Click(object sender, RoutedEventArgs e)
    {
        // Cuando Re-generar está activo, "Pendientes" incluye también los que ya tienen vídeo.
        // Excluir siempre los que están generando ahora (Running) para no cancelarles al generar.
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems)
            item.IsSelected = _videoIncludeRegenerate
                ? item.Status != VideoStatus.Error && item.Status != VideoStatus.Running
                : !item.HasVideo && item.Status != VideoStatus.Error && item.Status != VideoStatus.Running;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void BtnVideoSelectErrors_Click(object sender, RoutedEventArgs e)
    {
        var allErrors = _videoSelectItems.Count(i => i.Status == VideoStatus.Error);
        // Forzar Re-generar para que los errores se puedan reintentar aunque tengan HasVideo=true parcial
        if (!_videoIncludeRegenerate && allErrors > 0)
        {
            _videoIncludeRegenerate = true;
            chkVideoRegenerate.IsChecked = true;
            _videoSelectView?.Refresh();
            // ChkVideoRegenerate_Changed no disparará el recálculo del color de filtro (guard de igualdad)
            TxtVideoFilter_TextChanged(txtVideoFilter, null!);
        }
        // FilterVideoItem siempre deja pasar los errores, así que todos son visibles.
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = item.Status == VideoStatus.Error;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void BtnVideoSelectOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _videoPanelDestFolder;
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            System.Diagnostics.Process.Start("explorer.exe", folder);
    }

    private async void BtnVideoSelectRefresh_Click(object sender, RoutedEventArgs e)
    {
        // Bloquear refresco durante generación: itemLookup en RunVideoGenerationAsync
        // referencia los mismos objetos VideoSelectItem de _videoSelectItems; si
        // repoblaráramos ahora, esos objetos quedarían desconectados de la UI.
        if (_videoPanelDestFolder is null || _videoRefreshing || _videoRunning) return;
        _videoRefreshing = true;
        // Invalidar caché antes del populate manual: el usuario espera ver el estado real del disco
        InvalidateRawFilesCache(_videoPanelDestFolder);
        UpdateVideoSelectButton();   // deshabilitar Refresh/selección via _videoRefreshing
        // Capturar estado del checkbox ANTES del populate para restaurarlo después
        var savedRegenerate = _videoIncludeRegenerate;
        try { await PopulateVideoSelectPanelAsync(_videoPanelDestFolder).ConfigureAwait(true); }
        finally
        {
            _videoRefreshing = false;
            // Restaurar Re-generar si populate lo reseteó implícitamente
            if (_videoIncludeRegenerate != savedRegenerate)
            {
                _videoIncludeRegenerate = savedRegenerate;
                chkVideoRegenerate.IsChecked = savedRegenerate;
                _videoSelectView?.Refresh();
                TxtVideoFilter_TextChanged(txtVideoFilter, null!);   // recalcular color del filtro
            }
            UpdateVideoSelectButton();   // restaurar botones según estado real
        }
    }

    private async void BtnVideoSelectGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_videoRunning) return;

        var selected = _videoSelectItems.Where(i => i.IsSelected).Select(i => i.BestFile).ToList();
        if (selected.Count == 0) { Log("🎥 Video: no hay partituras seleccionadas."); return; }

        var museScoreExe = _videoPanelMuseScoreExe;
        var extraArgs = _videoPanelExtraArgs ?? ResolveMuseScoreVideoArgs();
        var destFolder = _videoPanelDestFolder ?? string.Empty;

        if (string.IsNullOrWhiteSpace(museScoreExe))
        {
            DarkDialogService.ShowMessage(this,
                "MuseScore no disponible. Vuelve a abrir el panel.",
                "MuseScore no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        txtStatus.Text = "🎥 Generando vídeos...";
        txtVideoEta.Text = string.Empty;
        await RunVideoGenerationAsync(selected, museScoreExe, extraArgs, destFolder,
            CancellationToken.None).ConfigureAwait(true);
    }

    private void VideoSelectItem_CheckChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressItemCheckEvents) return;   // ignorar cambios individuales durante operaciones batch
        UpdateVideoSelectButton();
    }

    private void ChkVideoSelectHeader_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressVideoHeaderEvents) return;
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = true;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void ChkVideoSelectHeader_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressVideoHeaderEvents) return;
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = false;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void ChkVideoSelectHeader_Indeterminate(object sender, RoutedEventArgs e)
    {
        if (_suppressVideoHeaderEvents) return;
        // Indeterminate manual → seleccionar todos (ciclo: null→todos)
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems) item.IsSelected = true;
        _suppressItemCheckEvents = false;
        _suppressVideoHeaderEvents = true;
        chkVideoSelectHeader.IsChecked = true;
        _suppressVideoHeaderEvents = false;
        UpdateVideoSelectButton();
    }

    private void LstVideoSelect_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (lstVideoSelect.SelectedItem is not VideoSelectItem item) return;

        // Doble clic en ítem en curso: no hacer nada (el MP4 puede estar incompleto)
        if (item.Status == VideoStatus.Running)
        {
            Log($"⏳ {item.DisplayName}: generación en curso...");
            return;
        }

        // Doble clic en error: informar y seleccionar para reintento
        if (item.Status == VideoStatus.Error)
        {
            item.IsSelected = true;
            UpdateVideoSelectButton();
            Log($"⚠️ Error en: {item.DisplayName}. Ítem seleccionado para reintentar.");
            return;
        }

        // Doble clic en partitura con MP4: abrir el vídeo
        if (item.HasVideo)
        {
            var mp4 = ResolveVideoSiblingPath(item.BestFile);
            if (mp4 is not null && File.Exists(mp4))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mp4) { UseShellExecute = true }); }
                catch (Exception ex) { Log($"❌ No se pudo abrir el MP4: {ex.Message}"); }
                return;
            }
            // MP4 marcado pero no encontrado en disco: corregir estado automáticamente
            Log($"⚠️ No se encontró el MP4 en disco para: {item.DisplayName}. Marcando como pendiente.");
            item.HasVideo = false;
            item.Status = VideoStatus.None;
            _videoSelectView?.Refresh();
            UpdateVideoSelectButton();
            return;
        }

        item.IsSelected = !item.IsSelected;
        UpdateVideoSelectButton();
    }

    // ── Filtro y ordenación ─────────────────────────────────────────────────────

    private void TxtVideoFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _videoFilterText = txtVideoFilter.Text;
        _videoSelectView?.Refresh();
        UpdateVideoSelectButton();
        // Mostrar/ocultar botón × según haya texto en el filtro
        btnVideoFilterClear.Visibility = string.IsNullOrEmpty(_videoFilterText)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
        // Feedback visual: fondo rojo suave cuando el filtro no produce resultados
        if (!string.IsNullOrEmpty(_videoFilterText) && !VisibleItems.Any())
            txtVideoFilter.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x1A, 0x1A));
        else
            txtVideoFilter.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
        // Scroll al primer resultado cuando el filtro cambia (para no quedar con vista vacía)
        if (!string.IsNullOrEmpty(_videoFilterText))
        {
            var first = VisibleItems.FirstOrDefault();
            if (first is not null) lstVideoSelect.ScrollIntoView(first);
        }
    }

    private void BtnVideoFilterClear_Click(object sender, RoutedEventArgs e)
    {
        txtVideoFilter.Clear();
        txtVideoFilter.Focus();
    }

    private void TxtVideoFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            // Escape en el cuadro de filtro: limpiar texto y devolver foco a la lista
            if (!string.IsNullOrEmpty(txtVideoFilter.Text))
            {
                txtVideoFilter.Clear();
                lstVideoSelect.Focus();
                e.Handled = true;
                return;
            }
        }
        if (e.Key != System.Windows.Input.Key.Enter) return;
        var first = VisibleItems.FirstOrDefault();
        if (first is not null)
        {
            lstVideoSelect.SelectedItem = first;
            lstVideoSelect.ScrollIntoView(first);
            lstVideoSelect.Focus();
        }
        e.Handled = true;
    }

    private void ChkVideoRegenerate_Changed(object sender, RoutedEventArgs e)
    {
        var newVal = chkVideoRegenerate.IsChecked == true;
        if (_videoIncludeRegenerate == newVal) return;  // evita doble refresh cuando se pone desde código
        _videoIncludeRegenerate = newVal;
        _videoSelectView?.Refresh();
        UpdateVideoSelectButton();
        // Recalcular color del fondo del filtro: puede haber cambiado visibilidad
        TxtVideoFilter_TextChanged(txtVideoFilter, null!);
    }

    private void LstVideoSelect_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.GridViewColumnHeader header
            || header.Column is null) return;

        // Restaurar el indicador de la columna anterior antes de leer el contenido actual
        if (_lastSortHeader is not null && _lastSortOriginalContent is not null)
            _lastSortHeader.Content = _lastSortOriginalContent;

        // Extraer texto del header (puede ser string o TextBlock con tooltip)
        var rawContent = header.Content;
        var content = rawContent is System.Windows.Controls.TextBlock tb
            ? tb.Text
            : rawContent?.ToString() ?? string.Empty;
        // Quitar indicador de orden anterior si el header fue previamente marcado
        if (content.EndsWith(" ▲", StringComparison.Ordinal) || content.EndsWith(" ▼", StringComparison.Ordinal))
            content = content[..^2];
        var propName = content switch
        {
            "Est" => nameof(VideoSelectItem.Status),
            "Nombre" => nameof(VideoSelectItem.DisplayName),
            "Formato" => nameof(VideoSelectItem.FormatTag),
            "Carpeta" => nameof(VideoSelectItem.FolderShort),
            "▶" => nameof(VideoSelectItem.HasVideo),   // ordenar por tiene/no-tiene MP4
            _ => null
        };
        if (propName is null || _videoSelectView is null) return;

        if (_videoSortColumn == propName)
            _videoSortAscending = !_videoSortAscending;
        else
        {
            _videoSortColumn = propName;
            _videoSortAscending = true;
        }

        // Guardar contenido original (puede ser TextBlock u objeto) y añadir indicador ▲/▼
        _lastSortHeader = header;
        _lastSortOriginalContent = rawContent;   // guardar el objeto original (TextBlock preserva tooltip)
        header.Content = _videoSortAscending ? $"{content} ▲" : $"{content} ▼";

        _videoSelectView.SortDescriptions.Clear();
        // Cuando el grouping está activo, preservar FolderShort como primera clave de orden
        // para que los grupos queden siempre ordenados alfabéticamente.
        if (_videoSelectView.GroupDescriptions.Count > 0 && propName != nameof(VideoSelectItem.FolderShort))
            _videoSelectView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                nameof(VideoSelectItem.FolderShort), System.ComponentModel.ListSortDirection.Ascending));
        _videoSelectView.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            propName, _videoSortAscending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending));
    }

    // ── Menú contextual ─────────────────────────────────────────────────────────

    private void LstVideoSelect_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Solo forzar selección si el ítem bajo el cursor no estaba seleccionado;
        // así se preserva la multi-selección cuando el usuario ya la tenía hecha.
        var element = e.OriginalSource as System.Windows.DependencyObject;
        while (element is not null && element is not System.Windows.Controls.ListViewItem)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        if (element is System.Windows.Controls.ListViewItem lvi)
        {
            _ctxMenuTargetItem = lvi.DataContext as VideoSelectItem;
            if (!lvi.IsSelected) lvi.IsSelected = true;
        }
        else
        {
            _ctxMenuTargetItem = null;
        }

        // Actualizar estado de menú contextual según si el ítem bajo cursor tiene MP4
        // Si hay multi-selección, habilitar si ALGUNO de los seleccionados tiene MP4
        var allSelected = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToList();
        bool anyHasVid = _ctxMenuTargetItem?.HasVideo == true
                          || allSelected.Any(i => i.HasVideo);
        bool anyRunning = _ctxMenuTargetItem?.Status == VideoStatus.Running
                          || allSelected.Any(i => i.Status == VideoStatus.Running);
        mnuCtxVideoOpen.IsEnabled = _ctxMenuTargetItem?.HasVideo ?? false;
        mnuCtxVideoOpenInMuseScore.IsEnabled = _ctxMenuTargetItem is not null
            && _ctxMenuTargetItem.Status != VideoStatus.Running
            && !string.IsNullOrWhiteSpace(_videoPanelMuseScoreExe);
        mnuCtxVideoDelete.IsEnabled = anyHasVid && !anyRunning;
        // Limpiar error: solo cuando alguno de los afectados tiene Status==Error
        bool anyError = _ctxMenuTargetItem?.Status == VideoStatus.Error
                        || allSelected.Any(i => i.Status == VideoStatus.Error);
        mnuCtxVideoClearError.IsEnabled = anyError && !anyRunning;
        // Contar ítems con error para label dinámico
        var clearErrCount = allSelected.Count(i => i.Status == VideoStatus.Error);
        if (_ctxMenuTargetItem is not null && _ctxMenuTargetItem.Status == VideoStatus.Error && !allSelected.Contains(_ctxMenuTargetItem)) clearErrCount++;
        mnuCtxVideoClearError.Header = clearErrCount > 1 ? $"↩ Limpiar error ({clearErrCount})" : "↩ Limpiar error";
        mnuCtxVideoGenerateSingle.IsEnabled = !_videoRunning;
        // Etiqueta dinámica: singular / plural según selección
        var genCount = allSelected.Count;
        if (_ctxMenuTargetItem is not null && !allSelected.Contains(_ctxMenuTargetItem)) genCount++;
        mnuCtxVideoGenerateSingle.Header = genCount > 1 ? $"🎥 Generar estos ({genCount})" : "🎥 Generar este";
        // Eliminar: contar ítems con MP4
        var delCount = allSelected.Count(i => i.HasVideo);
        if (_ctxMenuTargetItem is not null && _ctxMenuTargetItem.HasVideo && !allSelected.Contains(_ctxMenuTargetItem)) delCount++;
        mnuCtxVideoDelete.Header = delCount > 1 ? $"🗑 Eliminar MP4 ({delCount})" : "🗑 Eliminar MP4";
        mnuCtxVideoCopyMp4Path.IsEnabled = delCount > 0;
        mnuCtxVideoCopyMp4Path.Header = delCount > 1 ? $"📋 Copiar rutas de MP4 ({delCount})" : "📋 Copiar ruta de MP4";
        // Seleccionar toda la carpeta: sólo relevante cuando hay más de una carpeta y hay ítem target
        if (mnuCtxVideoSelectFolder is not null)
        {
            var folder = _ctxMenuTargetItem?.FolderShort;
            var folderCount = folder is not null
                ? VisibleItems.Count(i => string.Equals(i.FolderShort, folder, StringComparison.OrdinalIgnoreCase))
                : 0;
            mnuCtxVideoSelectFolder.IsEnabled = folderCount > 0 && !_videoRunning;
            mnuCtxVideoSelectFolder.Header = folderCount > 1
                ? $"📁 Seleccionar carpeta ({folderCount})"
                : "📁 Seleccionar toda la carpeta";
        }
        var visibleWithMp4 = VisibleItems.Count(i => i.HasVideo);
        mnuCtxVideoCopyAllMp4.Header    = visibleWithMp4 > 0 ? $"📋 Copiar rutas MP4 visibles ({visibleWithMp4})" : "📋 Copiar rutas MP4 visibles";
        mnuCtxVideoCopyAllMp4.IsEnabled = visibleWithMp4 > 0;
        // Seleccionar / Deseleccionar: mostrar cuenta si hay multi-selección
        var selDesCount = genCount;   // misma base que "Generar"
        mnuCtxVideoSelect.Header   = selDesCount > 1 ? $"☑ Seleccionar ({selDesCount})"   : "☑ Seleccionar";
        mnuCtxVideoDeselect.Header = selDesCount > 1 ? $"☐ Deseleccionar ({selDesCount})" : "☐ Deseleccionar";
        // Copiar ruta: mostrar cuenta si hay multi-selección
        mnuCtxVideoCopyPath.Header = selDesCount > 1 ? $"📋 Copiar rutas ({selDesCount})" : "📋 Copiar ruta";
    }

    private void CtxVideoOpen_Click(object sender, RoutedEventArgs e)
    {
        var item = _ctxMenuTargetItem ?? lstVideoSelect.SelectedItem as VideoSelectItem;
        if (item is null) return;
        var mp4 = ResolveVideoSiblingPath(item.BestFile);
        if (mp4 is null || !File.Exists(mp4))
        {
            Log($"⚠️ No se encontró MP4 para: {item.DisplayName}. Marcando como pendiente.");
            item.HasVideo = false;
            item.Status = VideoStatus.None;
            _videoSelectView?.Refresh();
            UpdateVideoSelectButton();
            return;
        }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mp4) { UseShellExecute = true }); }
        catch (Exception ex) { Log($"❌ No se pudo abrir el MP4: {ex.Message}"); }
    }

    private void CtxVideoDelete_Click(object sender, RoutedEventArgs e)
    {
        // Incluir _ctxMenuTargetItem aunque no esté en SelectedItems (clic derecho sin seleccionar)
        var targetSet = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targetSet.Add(_ctxMenuTargetItem);
        var targets = targetSet.Where(i => i.HasVideo && i.Status != VideoStatus.Running).ToList();
        if (targets.Count == 0) { Log("⚠️ Ningún ítem seleccionado tiene MP4."); return; }
        const int maxNamesInDialog = 5;
        var nameLines = targets.Count <= maxNamesInDialog
            ? string.Join("\n• ", targets.Select(i => i.DisplayName))
            : string.Join("\n• ", targets.Take(maxNamesInDialog).Select(i => i.DisplayName))
              + $"\n… y {targets.Count - maxNamesInDialog} más";
        var result = DarkDialogService.ShowMessage(this,
            $"¿Eliminar el MP4 de {targets.Count} partitura(s)?\n• {nameLines}",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        foreach (var item in targets)
        {
            var mp4 = ResolveVideoSiblingPath(item.BestFile);
            if (mp4 is null || !File.Exists(mp4))
            {
                // MP4 ya no existe: corregir estado obsoleto
                item.HasVideo = false;
                item.Status = VideoStatus.None;
                Log($"ℹ️ MP4 no encontrado para {item.DisplayName}. Estado corregido.");
                continue;
            }
            try
            {
                File.Delete(mp4);
                item.HasVideo = false;
                item.Status = VideoStatus.None;
                Log($"🗑 MP4 eliminado: {mp4}");
            }
            catch (Exception ex) { Log($"❌ No se pudo eliminar {mp4}: {ex.Message}"); }
        }
        _videoSelectView?.Refresh();
        UpdateVideoSelectButton();
        // Invalidar caché de archivos para que un Refresh posterior detecte los MP4 eliminados
        if (!string.IsNullOrEmpty(_videoPanelDestFolder))
            InvalidateRawFilesCache(_videoPanelDestFolder);
    }

    private async void CtxVideoGenerateSingle_Click(object sender, RoutedEventArgs e)
    {
        if (_videoRunning) return;
        // Incluir el ítem bajo el cursor aunque no esté en la selección múltiple
        var targetSet = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targetSet.Add(_ctxMenuTargetItem);
        var targets = targetSet
            .Where(i => i.Status != VideoStatus.Running && (!i.HasVideo || _videoIncludeRegenerate))
            .Select(i => i.BestFile).ToList();
        if (targets.Count == 0)
        {
            // Si todos los ítems tienen vídeo y Re-generar está apagado, auto-habilitarlo
            bool allHaveVideo = targetSet.All(i => i.HasVideo);
            if (allHaveVideo && !_videoIncludeRegenerate)
            {
                Log("ℹ️ Re-generar activado automáticamente (todos los ítems ya tienen MP4).");
                _videoIncludeRegenerate = true;
                chkVideoRegenerate.IsChecked = true;
                _videoSelectView?.Refresh();
                TxtVideoFilter_TextChanged(txtVideoFilter, null!);   // recalcular color del filtro
                targets = targetSet
                    .Where(i => i.Status != VideoStatus.Running)
                    .Select(i => i.BestFile).ToList();
            }
            if (targets.Count == 0)
            {
                Log("⚠️ Todos los ítems seleccionados ya están generando.");
                return;
            }
        }

        var museScoreExe = _videoPanelMuseScoreExe;
        if (string.IsNullOrWhiteSpace(museScoreExe))
        { Log("⚠️ MuseScore no disponible."); return; }

        txtStatus.Text = $"🎥 Generando {targets.Count} vídeo(s)…";
        txtVideoEta.Text = string.Empty;
        await RunVideoGenerationAsync(targets, museScoreExe,
            _videoPanelExtraArgs ?? ResolveMuseScoreVideoArgs(),
            _videoPanelDestFolder ?? string.Empty,
            CancellationToken.None).ConfigureAwait(true);
    }

    private void CtxVideoClearError_Click(object sender, RoutedEventArgs e)
    {
        var targets = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targets.Add(_ctxMenuTargetItem);
        var errors = targets.Where(i => i.Status == VideoStatus.Error).ToList();
        if (errors.Count == 0) { Log("ℹ️ Ningún ítem seleccionado tiene error."); return; }
        foreach (var item in errors)
            item.Status = VideoStatus.None;
        _videoSelectView?.Refresh();
        UpdateVideoSelectButton();
        Log($"↩ {errors.Count} ítem(s) con error restablecido(s) a pendiente.");
    }

    private void CtxVideoSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _ctxMenuTargetItem?.FolderShort;
        if (folder is null) return;
        _suppressItemCheckEvents = true;
        foreach (var item in VisibleItems)
        {
            if (string.Equals(item.FolderShort, folder, StringComparison.OrdinalIgnoreCase))
                item.IsSelected = true;
        }
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
        Log($"📁 Seleccionados todos los ítems de la carpeta: {folder}");
    }

    private void CtxVideoSelectOne_Click(object sender, RoutedEventArgs e)
    {
        var targets = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targets.Add(_ctxMenuTargetItem);
        _suppressItemCheckEvents = true;
        foreach (var item in targets) item.IsSelected = true;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void CtxVideoDeselectOne_Click(object sender, RoutedEventArgs e)
    {
        var targets = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targets.Add(_ctxMenuTargetItem);
        _suppressItemCheckEvents = true;
        foreach (var item in targets) item.IsSelected = false;
        _suppressItemCheckEvents = false;
        UpdateVideoSelectButton();
    }

    private void CtxVideoOpenInMuseScore_Click(object sender, RoutedEventArgs e)
    {
        var item = _ctxMenuTargetItem ?? lstVideoSelect.SelectedItem as VideoSelectItem;
        if (item is null) return;
        var exe = _videoPanelMuseScoreExe;
        if (string.IsNullOrWhiteSpace(exe)) { Log("⚠️ MuseScore no disponible."); return; }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, $"\"{item.BestFile}\"")
            {
                UseShellExecute = false
            });
            Log($"🎼 Abierto en MuseScore: {item.DisplayName}");
        }
        catch (Exception ex) { Log($"❌ No se pudo abrir en MuseScore: {ex.Message}"); }
    }

    private void CtxVideoOpenItemFolder_Click(object sender, RoutedEventArgs e)
    {
        var item = _ctxMenuTargetItem ?? lstVideoSelect.SelectedItem as VideoSelectItem;
        if (item is null) return;
        try
        {
            // /select resalta el fichero en lugar de solo abrir la carpeta
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.BestFile}\"");
        }
        catch (Exception ex) { Log($"❌ No se pudo abrir carpeta: {ex.Message}"); }
    }

    private void CtxVideoCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var targets = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targets.Add(_ctxMenuTargetItem);
        if (targets.Count == 0) return;
        try
        {
            var text = string.Join(Environment.NewLine, targets.Select(i => i.BestFile));
            System.Windows.Clipboard.SetText(text);
            if (targets.Count == 1)
                Log($"📋 Ruta copiada: {targets.First().BestFile}");
            else
                Log($"📋 {targets.Count} rutas copiadas al portapapeles.");
        }
        catch (Exception ex) { Log($"❌ No se pudo copiar al portapapeles: {ex.Message}"); }
    }

    private void CtxVideoCopyMp4Path_Click(object sender, RoutedEventArgs e)
    {
        var targetSet = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToHashSet();
        if (_ctxMenuTargetItem is not null) targetSet.Add(_ctxMenuTargetItem);
        if (targetSet.Count == 0) return;

        var copied = new List<string>();
        var fixedState = 0;
        foreach (var item in targetSet)
        {
            if (!item.HasVideo) continue;
            var mp4 = ResolveVideoSiblingPath(item.BestFile);
            if (mp4 is not null)
            {
                copied.Add(mp4);
                continue;
            }
            Log($"⚠️ No se encontró ruta de MP4 para: {item.DisplayName}. Marcando como pendiente.");
            item.HasVideo = false;
            item.Status = VideoStatus.None;
            fixedState++;
        }

        if (fixedState > 0)
        {
            _videoSelectView?.Refresh();
            UpdateVideoSelectButton();
        }

        if (copied.Count == 0)
        {
            Log("⚠️ Ningún ítem seleccionado tiene ruta MP4 disponible.");
            return;
        }

        try
        {
            var text = string.Join(Environment.NewLine, copied);
            System.Windows.Clipboard.SetText(text);
            if (copied.Count == 1)
                Log($"📋 Ruta MP4 copiada: {copied[0]}");
            else
                Log($"📋 {copied.Count} rutas MP4 copiadas al portapapeles.");
        }
        catch (Exception ex) { Log($"❌ No se pudo copiar al portapapeles: {ex.Message}"); }
    }

    private void CtxVideoCopyAllMp4Paths_Click(object sender, RoutedEventArgs e)
    {
        var paths = VisibleItems
            .Where(i => i.HasVideo)
            .Select(i => ResolveVideoSiblingPath(i.BestFile))
            .OfType<string>()
            .Where(File.Exists)
            .ToList();
        if (paths.Count == 0) { Log("⚠️ Ningún ítem visible tiene MP4 en disco."); return; }
        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, paths));
            Log($"📋 {paths.Count} ruta(s) MP4 copiadas al portapapeles.");
        }
        catch (Exception ex) { Log($"❌ No se pudo copiar al portapapeles: {ex.Message}"); }
    }

    private void LstVideoSelect_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            CtxVideoDelete_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F5)
        {
            // F5: volver a escanear la carpeta (Actualizar)
            if (btnVideoSelectRefresh.IsEnabled)
                BtnVideoSelectRefresh_Click(btnVideoSelectRefresh, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Space)
        {
            var targets = lstVideoSelect.SelectedItems.Cast<VideoSelectItem>().ToList();
            // Si alguno está desmarcado → marcar todos; si todos marcados → desmarcar todos
            bool selectAll = targets.Any(i => !i.IsSelected);
            _suppressItemCheckEvents = true;
            foreach (var item in targets)
                item.IsSelected = selectAll;
            _suppressItemCheckEvents = false;
            UpdateVideoSelectButton();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.A
                 && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            // Ctrl+A: seleccionar todos los ítems visibles en la lista
            _suppressItemCheckEvents = true;
            foreach (var item in VisibleItems) item.IsSelected = true;
            _suppressItemCheckEvents = false;
            UpdateVideoSelectButton();
            e.Handled = true;
        }
        else if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.I:
                    // Ctrl+I: invertir selección (solo visibles)
                    if (btnVideoSelectInvert.IsEnabled)
                        BtnVideoSelectInvert_Click(btnVideoSelectInvert, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.P:
                    // Ctrl+P: seleccionar pendientes
                    if (btnVideoSelectPending.IsEnabled)
                        BtnVideoSelectPending_Click(btnVideoSelectPending, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.E:
                    // Ctrl+E: seleccionar errores
                    if (btnVideoSelectErrors.IsEnabled)
                        BtnVideoSelectErrors_Click(btnVideoSelectErrors, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.R:
                    // Ctrl+R: activar/desactivar Re-generar (toggle)
                    if (chkVideoRegenerate.IsEnabled)
                    {
                        chkVideoRegenerate.IsChecked = !_videoIncludeRegenerate;
                        // ChkVideoRegenerate_Changed se dispara automáticamente
                    }
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.O:
                    // Ctrl+O: abrir carpeta de destino en el Explorador
                    if (btnVideoSelectOpenFolder.IsEnabled)
                        BtnVideoSelectOpenFolder_Click(btnVideoSelectOpenFolder, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.G:
                    // Ctrl+G: generar
                    if (btnVideoSelectGenerate.IsEnabled)
                        BtnVideoSelectGenerate_Click(btnVideoSelectGenerate, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == System.Windows.Input.Key.Return)
        {
            // Enter: abrir MP4 del ítem enfocado (equivale a doble clic)
            var item = lstVideoSelect.SelectedItem as VideoSelectItem;
            if (item?.HasVideo == true)
                LstVideoSelect_MouseDoubleClick(sender, null!);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (!string.IsNullOrEmpty(txtVideoFilter.Text))
                txtVideoFilter.Clear();
            else if (_videoRunning && btnVideoSelectCancelGen.IsEnabled)
                BtnCancelVideo_Click(btnVideoSelectCancelGen, new RoutedEventArgs());
            else if (!_videoRunning)
                BtnVideoSelectBack_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // Calcula el texto de tooltip del tamaño del MP4 para un archivo de partitura
    private static string ComputeMp4SizeToolTip(string inputPath)
    {
        var mp4 = ResolveVideoSiblingPath(inputPath);
        if (mp4 is null) return string.Empty;   // sin MP4 real, no mostrar texto engañoso
        try
        {
            var bytes = new FileInfo(mp4).Length;
            var mb = bytes / (1024.0 * 1024.0);
            var size = mb >= 1024 ? $"{mb / 1024:F1} GB" : $"{mb:F1} MB";
            return $"Ya tiene vídeo MP4 · {size}";
        }
        catch { return "Ya tiene vídeo MP4"; }
    }

    // Enumera las rutas candidatas del MP4 hermano (directa y en subcarpeta)
    private static IEnumerable<string> VideoSiblingCandidates(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath);
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem)) yield break;
        yield return Path.Combine(dir, stem + ".mp4");
        yield return Path.Combine(dir, stem, stem + ".mp4");
    }

    // Resuelve la ruta del MP4 hermano de un archivo de partitura
    private static string? ResolveVideoSiblingPath(string inputPath)
        => VideoSiblingCandidates(inputPath).FirstOrDefault(File.Exists);

    private static bool HasVideoSibling(string inputPath)
        => VideoSiblingCandidates(inputPath).Any(File.Exists);

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

    private string? TryCreateSubtitleStyleOverrideFile()
    {
        if (_videoSubtitleFontPt <= 0)
            return null;

        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "ScoreDown");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"video-subtitle-{_videoSubtitleFontPt}.mss");
            var styleXml = $"""
<?xml version="1.0" encoding="UTF-8"?>
<museScore version="4.00">
  <Style>
    <subTitleFontSize>{_videoSubtitleFontPt}</subTitleFontSize>
  </Style>
</museScore>
""";

            if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), styleXml, StringComparison.Ordinal))
                File.WriteAllText(path, styleXml, Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static string StripMusicXmlVisualHeaderMetadata(string xmlText)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xmlText);

        static void RemoveByXPath(XmlDocument d, string xpath)
        {
            var nodes = d.SelectNodes(xpath);
            if (nodes is null) return;
            var snapshot = new List<XmlNode>();
            foreach (XmlNode n in nodes) snapshot.Add(n);
            foreach (var n in snapshot)
                n.ParentNode?.RemoveChild(n);
        }

        // Remove elements MuseScore usually renders as page-title/header/footer credits.
        RemoveByXPath(doc, "//*[local-name()='work-title']");
        RemoveByXPath(doc, "//*[local-name()='movement-title']");
        RemoveByXPath(doc, "//*[local-name()='credit']");
        RemoveByXPath(doc, "//*[local-name()='identification']/*[local-name()='creator']");
        RemoveByXPath(doc, "//*[local-name()='identification']/*[local-name()='rights']");

        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    private async Task<string?> TryCreateSanitizedVideoInputAsync(string inputPath, CancellationToken ct)
    {
        if (!_videoHideOriginalScoreTitle)
            return null;

        var ext = Path.GetExtension(inputPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "ScoreDown", "video-input");
        Directory.CreateDirectory(tempRoot);

        if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase) || ext.Equals(".musicxml", StringComparison.OrdinalIgnoreCase))
        {
            var xmlText = await File.ReadAllTextAsync(inputPath, ct).ConfigureAwait(false);
            var sanitized = StripMusicXmlVisualHeaderMetadata(xmlText);
            var tmpPath = Path.Combine(tempRoot, $"{Path.GetFileNameWithoutExtension(inputPath)}.{Guid.NewGuid():N}{ext}");
            await File.WriteAllTextAsync(tmpPath, sanitized, Encoding.UTF8, ct).ConfigureAwait(false);
            return tmpPath;
        }

        if (!ext.Equals(".mxl", StringComparison.OrdinalIgnoreCase))
            return null;

        var tmpMxl = Path.Combine(tempRoot, $"{Path.GetFileNameWithoutExtension(inputPath)}.{Guid.NewGuid():N}.mxl");
        using (var src = ZipFile.OpenRead(inputPath))
        using (var dstFs = File.Create(tmpMxl))
        using (var dst = new ZipArchive(dstFs, ZipArchiveMode.Create))
        {
            var scoreXmlEntry = src.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && !e.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase));

            foreach (var entry in src.Entries)
            {
                var outEntry = dst.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                await using var inStream = entry.Open();
                await using var outStream = outEntry.Open();

                if (scoreXmlEntry is not null && string.Equals(entry.FullName, scoreXmlEntry.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(inStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    var xmlText = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                    var sanitized = StripMusicXmlVisualHeaderMetadata(xmlText);
                    await using var writer = new StreamWriter(outStream, new UTF8Encoding(false), 1024, leaveOpen: true);
                    await writer.WriteAsync(sanitized.AsMemory(), ct).ConfigureAwait(false);
                    await writer.FlushAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    await inStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
                }
            }
        }

        return tmpMxl;
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

    private static async Task<string?> ResolveFfmpegExecutableAsync()
    {
        var env = Environment.GetEnvironmentVariable("FFMPEG_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var candidates = new[]
        {
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
        };

        foreach (var path in candidates)
            if (File.Exists(path))
                return path;

        if (await CanExecuteAsync("ffmpeg", "-version").ConfigureAwait(false))
            return "ffmpeg";

        return null;
    }

    private static async Task<string?> ResolveFfprobeExecutableAsync()
    {
        var env = Environment.GetEnvironmentVariable("FFPROBE_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var candidates = new[]
        {
            @"C:\ProgramData\chocolatey\bin\ffprobe.exe",
            @"C:\ffmpeg\bin\ffprobe.exe",
            @"C:\Program Files\ffmpeg\bin\ffprobe.exe"
        };

        foreach (var path in candidates)
            if (File.Exists(path))
                return path;

        if (await CanExecuteAsync("ffprobe", "-version").ConfigureAwait(false))
            return "ffprobe";

        return null;
    }

    private static async Task<double?> GetMediaDurationSecondsAsync(string ffprobeExe, string inputPath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffprobeExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            psi.ArgumentList.Add(inputPath);

            using var p = new Process { StartInfo = psi };
            if (!p.Start()) return null;

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            _ = await stderrTask.ConfigureAwait(false);

            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                return null;

            if (double.TryParse(stdout, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                return seconds;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed record VideoPresentationMeta(string Title, string Subtitle, string Authors);

    private enum VideoStatus { None, Running, Done, Error }

    private sealed class VideoSelectItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _hasVideo;
        private VideoStatus _status;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                SelectionDelta?.Invoke(value ? 1 : -1);
                Notify(nameof(IsSelected));
            }
        }

        // Callback para que el padre (MainWindow) mantenga _videoTotalSelected en O(1)
        internal Action<int>? SelectionDelta;

        public bool HasVideo
        {
            get => _hasVideo;
            set
            {
                if (_hasVideo == value) return;
                _hasVideo = value;
                Notify(nameof(HasVideo));
                Notify(nameof(HasVideoMark));
                if (!value) { _mp4SizeToolTip = string.Empty; Notify(nameof(Mp4SizeToolTip)); }
            }
        }

        public VideoStatus Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                Notify(nameof(Status));
                Notify(nameof(StatusIcon));
                Notify(nameof(StatusColor));
                Notify(nameof(StatusLabel));
            }
        }

        public string StatusIcon => _status switch
        {
            VideoStatus.Running => "⏳",
            VideoStatus.Done => "✅",
            VideoStatus.Error => "❌",
            _ => string.Empty
        };

        public System.Windows.Media.Brush StatusColor => _status switch
        {
            VideoStatus.Running => System.Windows.Media.Brushes.Gold,
            VideoStatus.Done => System.Windows.Media.Brushes.LimeGreen,
            VideoStatus.Error => System.Windows.Media.Brushes.OrangeRed,
            _ => System.Windows.Media.Brushes.Transparent
        };

        public string StatusLabel => _status switch
        {
            VideoStatus.Running => "En curso…",
            VideoStatus.Done => "Completado",
            VideoStatus.Error => "Error",
            _ => string.Empty
        };

        public string BestFile { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string FormatTag { get; init; } = string.Empty;
        public System.Windows.Media.Brush FormatColor { get; init; } = System.Windows.Media.Brushes.DimGray;
        public string HasVideoMark => _hasVideo ? "✓" : string.Empty;
        public string FolderShort { get; init; } = string.Empty;
        public string FolderFull => Path.GetDirectoryName(BestFile) ?? string.Empty;

        private string _mp4SizeToolTip = string.Empty;
        public string Mp4SizeToolTip
        {
            get => _mp4SizeToolTip;
            set { if (_mp4SizeToolTip == value) return; _mp4SizeToolTip = value; Notify(nameof(Mp4SizeToolTip)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    // Priority order for "best quality" file selection per score stem
    private static readonly string[] VideoFormatPriority =
        { ".mscz", ".mxl", ".musicxml", ".xml" };

    private static string EscapeFfmpegDrawText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string WrapTextForOverlay(string value, int maxCharsPerLine, int maxLines)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Length > maxCharsPerLine)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    if (lines.Count >= maxLines)
                        return string.Join("\n", lines);
                    current.Clear();
                }

                lines.Add(word);
                if (lines.Count >= maxLines)
                    return string.Join("\n", lines);
                continue;
            }

            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= maxCharsPerLine)
            {
                current.Append(' ').Append(word);
                continue;
            }

            lines.Add(current.ToString());
            if (lines.Count >= maxLines)
                return string.Join("\n", lines);

            current.Clear();
            current.Append(word);
        }

        if (current.Length > 0 && lines.Count < maxLines)
            lines.Add(current.ToString());

        if (lines.Count == 0)
            lines.Add(text);

        if (lines.Count > maxLines)
            lines = lines.Take(maxLines).ToList();

        return string.Join("\n", lines);
    }

    private static string? ResolveElegantFontForDrawText()
    {
        var env = Environment.GetEnvironmentVariable("SCOREDOWN_VIDEO_TITLE_FONTFILE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env.Replace("\\", "/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);

        var candidates = new[]
        {
            @"C:\Windows\Fonts\GARA.TTF",
            @"C:\Windows\Fonts\Garamond.ttf",
            @"C:\Windows\Fonts\times.ttf",
            @"C:\Windows\Fonts\BOD_R.TTF",
            @"C:\Windows\Fonts\georgia.ttf"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path.Replace("\\", "/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);
        }

        return null;
    }

    private static string CreateTempOverlayTextFile(string content, string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ScoreDown", "video-overlay-text");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path.Replace("\\", "/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);
    }

    private static string EscapeFfmpegFilterPath(string path)
        => path.Replace("\\", "/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);

    private static string? ResolveLuxuryLogoOverlayFile()
    {
        var env = Environment.GetEnvironmentVariable("SCOREDOWN_VIDEO_LOGO_FILE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var candidates = new List<string>();

        void AddCandidate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            candidates.Add(path);
        }

        var baseDir = AppContext.BaseDirectory;
        AddCandidate(Path.Combine(baseDir, "logo_scores.png"));
        AddCandidate(Path.Combine(baseDir, "preview", "logo_scores.png"));

        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            AddCandidate(Path.Combine(dir.FullName, "preview", "logo_scores.png"));
            AddCandidate(Path.Combine(dir.FullName, "ScoreDown", "preview", "logo_scores.png"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string ClipText(string value, int maxLen)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLen) return text;
        return text[..Math.Max(0, maxLen - 1)] + "…";
    }

    private static VideoPresentationMeta BuildVideoPresentationMeta(string inputPath)
    {
        var fallbackTitle = Path.GetFileNameWithoutExtension(inputPath);
        var title = fallbackTitle;
        var subtitle = string.Empty;
        var authors = string.Empty;

        try
        {
            string? xmlText = null;
            var ext = Path.GetExtension(inputPath);
            if (ext.Equals(".mxl", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(inputPath);
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    && !e.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    xmlText = reader.ReadToEnd();
                }
            }
            else if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase) || ext.Equals(".musicxml", StringComparison.OrdinalIgnoreCase))
            {
                xmlText = File.ReadAllText(inputPath, Encoding.UTF8);
            }

            if (!string.IsNullOrWhiteSpace(xmlText))
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlText);

                var workTitle = doc.SelectSingleNode("//*[local-name()='work-title']")?.InnerText?.Trim();
                var movementTitle = doc.SelectSingleNode("//*[local-name()='movement-title']")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(workTitle) && !string.IsNullOrWhiteSpace(movementTitle))
                {
                    title = workTitle;
                    subtitle = movementTitle;
                }
                else if (!string.IsNullOrWhiteSpace(workTitle))
                {
                    title = workTitle;
                }
                else if (!string.IsNullOrWhiteSpace(movementTitle))
                {
                    title = movementTitle;
                }

                var creatorNodes = doc.SelectNodes("//*[local-name()='identification']/*[local-name()='creator']");
                var preferred = new List<string>();
                var secondary = new List<string>();
                if (creatorNodes is not null)
                {
                    foreach (XmlNode node in creatorNodes)
                    {
                        var value = node.InnerText?.Trim();
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        var type = (node.Attributes?["type"]?.Value ?? string.Empty).Trim().ToLowerInvariant();
                        if (type is "composer" or "arranger")
                            preferred.Add(value);
                        else if (type is "lyricist" or "poet")
                            secondary.Add(value);
                    }
                }

                var authorsRaw = preferred.Count > 0 ? preferred : secondary;
                if (authorsRaw.Count > 0)
                {
                    var unique = authorsRaw
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(6)
                        .ToArray();
                    authors = string.Join(" / ", unique);
                }
            }
        }
        catch
        {
            // fallback to filename-only title
        }

        title = ClipText(title, 220);
        subtitle = ClipText(subtitle, 220);
        authors = ClipText(authors, 180);
        return new VideoPresentationMeta(title, subtitle, authors);
    }

    private async Task<bool> TryApplyLuxuryTitleOverlayAsync(string outputMp4, string inputPath, CancellationToken ct)
    {
        if (!File.Exists(outputMp4))
            return false;

        var ffmpegExe = await ResolveFfmpegExecutableAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ffmpegExe))
            return false;

        var meta = BuildVideoPresentationMeta(inputPath);
        var titleText = WrapTextForOverlay(meta.Title, 22, 2);
        var subtitleText = WrapTextForOverlay(meta.Subtitle, 34, 2);
        var authorsText = string.IsNullOrWhiteSpace(meta.Authors) ? string.Empty : meta.Authors.Replace("\n", " ", StringComparison.Ordinal).Trim();
        var showAuthors = !string.IsNullOrWhiteSpace(authorsText);
        var secs = _videoLuxuryTitleSeconds;
        var fontPath = ResolveElegantFontForDrawText();
        var fontOpt = string.IsNullOrWhiteSpace(fontPath) ? string.Empty : $"fontfile='{fontPath}':";
        var logoFile = ResolveLuxuryLogoOverlayFile();
        var titleLines = string.IsNullOrWhiteSpace(titleText)
            ? Array.Empty<string>()
            : titleText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var subtitleLines = string.IsNullOrWhiteSpace(subtitleText)
            ? Array.Empty<string>()
            : subtitleText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Build drawtext filters for the portada PNG (static — no 'enable' filter needed)
        var filters = new List<string>
        {
            "drawbox=x=0:y=0:w=iw:h=ih:color=0x0b0704@1.0:t=fill",
            "drawbox=x=0:y=0:w=iw:h=6:color=0xD4AF37@0.95:t=fill",
            "drawbox=x=0:y=ih-6:w=iw:h=6:color=0xD4AF37@0.95:t=fill"
        };

        for (var i = 0; i < titleLines.Length; i++)
        {
            var titleFile = CreateTempOverlayTextFile(titleLines[i], $"title-{i}.txt");
            var titleY = 0.32 + (i * 0.12);
            filters.Add($"drawtext={fontOpt}textfile='{titleFile}':x=(w-text_w)/2:y=h*{titleY.ToString(System.Globalization.CultureInfo.InvariantCulture)}:fontsize=240:fontcolor=0xF0D98C:borderw=8:bordercolor=black@0.99:shadowx=4:shadowy=4:shadowcolor=black@0.90");
        }

        if (subtitleLines.Length > 0)
        {
            for (var i = 0; i < subtitleLines.Length; i++)
            {
                var subtitleFile = CreateTempOverlayTextFile(subtitleLines[i], $"subtitle-{i}.txt");
                var subtitleY = 0.62 + (i * 0.08);
                filters.Add($"drawtext={fontOpt}textfile='{subtitleFile}':x=(w-text_w)/2:y=h*{subtitleY.ToString(System.Globalization.CultureInfo.InvariantCulture)}:fontsize=148:fontcolor=0xF0D98C:borderw=6:bordercolor=black@0.99:shadowx=4:shadowy=4:shadowcolor=black@0.86");
            }
        }

        if (showAuthors)
        {
            var authorsFile = CreateTempOverlayTextFile(authorsText, "authors.txt");
            filters.Add($"drawtext={fontOpt}textfile='{authorsFile}':x=(w-text_w)/2:y=h*0.83:fontsize=132:fontcolor=0xF0D98C:borderw=6:bordercolor=black@0.99:shadowx=4:shadowy=4:shadowcolor=black@0.86");
        }

        // Step 1: Generate portada PNG (3840×2160, dark background + text + logo)
        var portadaPng = outputMp4 + ".portada.png";
        try { if (File.Exists(portadaPng)) File.Delete(portadaPng); } catch { }

        bool portadaOk;
        if (logoFile is not null)
        {
            var fc = $"[0:v]{string.Join(",", filters)}[base];[1:v]scale=1260:-1:flags=lanczos[logo];[base][logo]overlay=x=(main_w-overlay_w)/2:y=50:format=auto[outv]";
            portadaOk = await RunProcessAsync(ffmpegExe,
                new List<string> { "-y", "-f", "lavfi", "-i", "color=c=0x0b0704:s=3840x2160:d=1", "-i", logoFile,
                    "-filter_complex", fc, "-map", "[outv]", "-frames:v", "1", portadaPng },
                "portada.png", "FFmpeg portada", TimeSpan.FromMinutes(2), ct).ConfigureAwait(false);
        }
        else
        {
            portadaOk = await RunProcessAsync(ffmpegExe,
                new List<string> { "-y", "-f", "lavfi", "-i", "color=c=0x0b0704:s=3840x2160:d=1",
                    "-vf", string.Join(",", filters), "-frames:v", "1", portadaPng },
                "portada.png", "FFmpeg portada", TimeSpan.FromMinutes(2), ct).ConfigureAwait(false);
        }

        if (!portadaOk || !File.Exists(portadaPng))
            return false;

        // Step 2: Encode portada as intro video (1920×1080, 30 fps, fade-in 1s / fade-out 1s)
        var introMp4 = outputMp4 + ".intro.tmp.mp4";
        try { if (File.Exists(introMp4)) File.Delete(introMp4); } catch { }
        var fadeOutSt = (secs - 1).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var secsStr = secs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var introVf = $"scale=1920:1080,fps=30,fade=t=in:st=0:d=1,fade=t=out:st={fadeOutSt}:d=1,format=yuv420p";
        var introOk = await RunProcessAsync(ffmpegExe,
            new List<string> { "-y", "-loop", "1", "-i", portadaPng,
                "-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=44100",
                "-vf", introVf, "-t", secsStr, "-r", "30",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                "-c:a", "aac", introMp4 },
            "intro.mp4", "FFmpeg intro", TimeSpan.FromMinutes(2), ct).ConfigureAwait(false);

        try { if (File.Exists(portadaPng)) File.Delete(portadaPng); } catch { }

        if (!introOk || !File.Exists(introMp4))
            return false;

        // Step 3: Normalize score video to 1920×1080, 30 fps (to match intro)
        var normMp4 = outputMp4 + ".norm.tmp.mp4";
        try { if (File.Exists(normMp4)) File.Delete(normMp4); } catch { }
        var normOk = await RunProcessAsync(ffmpegExe,
            new List<string> { "-y", "-i", outputMp4,
                "-vf", "scale=1920:1080,fps=30,format=yuv420p",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18",
                "-c:a", "aac", "-ar", "44100", "-ac", "2", normMp4 },
            "norm.mp4", "FFmpeg normalize", TimeSpan.FromMinutes(10), ct).ConfigureAwait(false);

        if (!normOk || !File.Exists(normMp4))
        {
            try { if (File.Exists(introMp4)) File.Delete(introMp4); } catch { }
            return false;
        }

        // Step 4: Concat intro + score via concat demuxer
        var concatList = outputMp4 + ".concat.txt";
        await File.WriteAllTextAsync(concatList,
            $"file '{introMp4.Replace("\\", "/", StringComparison.Ordinal)}'\nfile '{normMp4.Replace("\\", "/", StringComparison.Ordinal)}'",
            System.Text.Encoding.UTF8, ct).ConfigureAwait(false);

        var tmp = outputMp4 + ".lux.tmp.mp4";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        var concatOk = await RunProcessAsync(ffmpegExe,
            new List<string> { "-y", "-f", "concat", "-safe", "0", "-i", concatList,
                "-c", "copy", "-movflags", "+faststart", tmp },
            Path.GetFileName(outputMp4), "FFmpeg concat intro", TimeSpan.FromMinutes(5), ct).ConfigureAwait(true);

        foreach (var f in new[] { introMp4, normMp4, concatList })
            try { if (File.Exists(f)) File.Delete(f); } catch { }

        if (!concatOk || !File.Exists(tmp))
            return false;

        try
        {
            File.Move(tmp, outputMp4, true);
            return true;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    private async Task<bool> TryTrimVideoTailAsync(string outputMp4, int trimTailSeconds, CancellationToken ct)
    {
        if (trimTailSeconds <= 0 || !File.Exists(outputMp4))
            return false;

        var ffmpegExe = await ResolveFfmpegExecutableAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ffmpegExe))
        {
            Log("ℹ️ ffmpeg no encontrado: omitiendo recorte de cola de vídeo.");
            return false;
        }

        var ffprobeExe = await ResolveFfprobeExecutableAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ffprobeExe))
            return false;

        var duration = await GetMediaDurationSecondsAsync(ffprobeExe, outputMp4, ct).ConfigureAwait(false);
        if (duration is null)
            return false;

        var target = duration.Value - trimTailSeconds;
        if (target <= 2)
            return false;

        var tmp = outputMp4 + ".trim.tmp.mp4";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        var args = new List<string>
        {
            "-y",
            "-i", outputMp4,
            "-t", target.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            "-map", "0",
            "-c", "copy",
            "-movflags", "+faststart",
            tmp
        };

        var ok = await RunProcessAsync(ffmpegExe, args, Path.GetFileName(outputMp4), "FFmpeg trim", TimeSpan.FromMinutes(2), ct).ConfigureAwait(true);
        if (!ok || !File.Exists(tmp))
            return false;

        try
        {
            File.Move(tmp, outputMp4, true);
            return true;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    private async Task<bool> TryApplyFadeOutAsync(string outputMp4, int fadeSeconds, CancellationToken ct)
    {
        if (fadeSeconds <= 0 || !File.Exists(outputMp4))
            return false;

        var ffmpegExe = await ResolveFfmpegExecutableAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ffmpegExe))
            return false;

        var ffprobeExe = await ResolveFfprobeExecutableAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ffprobeExe))
            return false;

        var duration = await GetMediaDurationSecondsAsync(ffprobeExe, outputMp4, ct).ConfigureAwait(false);
        if (duration is null || duration.Value <= fadeSeconds + 2)
            return false;

        var fadeStart = (duration.Value - fadeSeconds).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var fadeDur = fadeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var tmp = outputMp4 + ".fadeout.tmp.mp4";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        var args = new List<string>
        {
            "-y",
            "-i", outputMp4,
            "-vf", $"fade=t=out:st={fadeStart}:d={fadeDur}",
            "-af", $"afade=t=out:st={fadeStart}:d={fadeDur}",
            "-c:v", "libx264",
            "-preset", "medium",
            "-crf", "18",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            tmp
        };

        var ok = await RunProcessAsync(ffmpegExe, args, Path.GetFileName(outputMp4), "FFmpeg fade-out", TimeSpan.FromMinutes(8), ct).ConfigureAwait(true);
        if (!ok || !File.Exists(tmp))
            return false;

        try
        {
            File.Move(tmp, outputMp4, true);
            return true;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    private async Task<bool> RunMuseScoreVideoAsync(string museScoreExe, string inputPath, string outputMp4, IReadOnlyList<string> extraVideoArgs, CancellationToken ct = default)
    {
        static List<string> BuildCommandArgs(string input, string output, IReadOnlyList<string> renderArgs, string? stylePath)
        {
            var args = new List<string>(renderArgs.Count + 7);
            args.Add("--score-video");
            args.AddRange(renderArgs);
            if (!string.IsNullOrWhiteSpace(stylePath))
            {
                args.Add("--style");
                args.Add(stylePath);
            }
            args.Add("-o");
            args.Add(output);
            args.Add(input);
            return args;
        }

        static List<string> BuildFastRetryArgs(IReadOnlyList<string> sourceArgs)
        {
            var result = new List<string>(sourceArgs);

            static void UpsertArg(List<string> args, string key, string value)
            {
                for (int i = 0; i < args.Count - 1; i++)
                {
                    if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    {
                        args[i + 1] = value;
                        return;
                    }
                }
                args.Add(key);
                args.Add(value);
            }

            // Retry profile: lower render cost to avoid long stalls/timeouts.
            UpsertArg(result, "--resolution", "1080p");
            UpsertArg(result, "--fps", "30");
            return result;
        }

        var inputName = Path.GetFileName(inputPath);
        var stylePath = TryCreateSubtitleStyleOverrideFile();
        var tempRenderInput = await TryCreateSanitizedVideoInputAsync(inputPath, ct).ConfigureAwait(false);
        var renderInputPath = string.IsNullOrWhiteSpace(tempRenderInput) ? inputPath : tempRenderInput;

        var primaryResolution = GetArgValue(extraVideoArgs, "--resolution") ?? "2160p";
        var primaryFps = GetArgValue(extraVideoArgs, "--fps") ?? "60";
        var primaryProfile = GetArgValue(extraVideoArgs, "--sound-profile") ?? "MuseSounds";
        Log($"🎛️ Perfil vídeo primario ({inputName}): res={primaryResolution}, fps={primaryFps}, audio={primaryProfile}, timeout={_videoTimeoutSeconds}s");
        if (!string.IsNullOrWhiteSpace(stylePath))
            LogDebug($"🎨 Estilo vídeo ({inputName}): subtítulo {_videoSubtitleFontPt}pt");
        if (!string.IsNullOrWhiteSpace(tempRenderInput))
            LogDebug($"🧹 Título original ocultado ({inputName}) para render.");

        int lastPrimaryPct = -1;
        Action<int> primaryProgress = pct =>
        {
            var clamped = Math.Clamp(pct, 0, 100);
            if (clamped == lastPrimaryPct) return;
            if (lastPrimaryPct >= 0 && clamped < lastPrimaryPct) return;
            if (lastPrimaryPct >= 0 && clamped - lastPrimaryPct < 2 && clamped != 100) return;
            lastPrimaryPct = clamped;
            _ = Dispatcher.InvokeAsync(() => txtStatus.Text = $"🎥 {inputName}: {clamped}%");
            LogDebug($"⏳ Video progreso ({inputName}): {clamped}%");
        };

        try
        {
            var mainArgs = BuildCommandArgs(renderInputPath, outputMp4, extraVideoArgs, stylePath);
            var primaryOk = await RunProcessAsync(
                museScoreExe,
                mainArgs,
                inputName,
                "MuseScore MP4",
                TimeSpan.FromSeconds(_videoTimeoutSeconds),
                ct,
                primaryProgress).ConfigureAwait(true);

            if (primaryOk && File.Exists(outputMp4))
                return true;

            if (ct.IsCancellationRequested)
                return false;

            // Fallback retry for heavy scores: faster settings + extended timeout.
            var retryArgs = BuildFastRetryArgs(extraVideoArgs);
            var retryTimeoutSec = Math.Min(_videoTimeoutSeconds * 2, 3600);
            var retryResolution = GetArgValue(retryArgs, "--resolution") ?? "1080p";
            var retryFps = GetArgValue(retryArgs, "--fps") ?? "30";
            var retryProfile = GetArgValue(retryArgs, "--sound-profile") ?? primaryProfile;
            Log($"ℹ️ Reintento vídeo modo rápido ({inputName}): res={retryResolution}, fps={retryFps}, audio={retryProfile}, timeout={retryTimeoutSec}s");

            int lastRetryPct = -1;
            Action<int> retryProgress = pct =>
            {
                var clamped = Math.Clamp(pct, 0, 100);
                if (clamped == lastRetryPct) return;
                if (lastRetryPct >= 0 && clamped < lastRetryPct) return;
                if (lastRetryPct >= 0 && clamped - lastRetryPct < 2 && clamped != 100) return;
                lastRetryPct = clamped;
                _ = Dispatcher.InvokeAsync(() => txtStatus.Text = $"🎥 {inputName}: retry {clamped}%");
                LogDebug($"⏳ Video progreso retry ({inputName}): {clamped}%");
            };

            try { if (File.Exists(outputMp4)) File.Delete(outputMp4); } catch { }

            var retryOk = await RunProcessAsync(
                museScoreExe,
                BuildCommandArgs(renderInputPath, outputMp4, retryArgs, stylePath),
                inputName,
                "MuseScore MP4 (retry)",
                TimeSpan.FromSeconds(retryTimeoutSec),
                ct,
                retryProgress).ConfigureAwait(true);

            return retryOk && File.Exists(outputMp4);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempRenderInput))
            {
                try { if (File.Exists(tempRenderInput)) File.Delete(tempRenderInput); } catch { }
            }
        }
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

    private async Task<bool> RunProcessAsync(string exe, IEnumerable<string> args, string inputName, string stage, TimeSpan? timeout = null, CancellationToken ct = default, Action<int>? onPercent = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
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

        LogDebug($"▶ {stage} start ({inputName}) exe={Path.GetFileName(exe)} timeout={(timeout?.TotalSeconds.ToString("0") ?? "∞")}s args={string.Join(' ', psi.ArgumentList)}");

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"No se pudo iniciar el proceso: {psi.FileName}");

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleOutputLine(StringBuilder sink, TaskCompletionSource<bool> closed, string? line)
        {
            if (line is null)
            {
                closed.TrySetResult(true);
                return;
            }

            lock (sink)
                sink.AppendLine(line);

            if (onPercent is not null && TryParsePercentFromText(line, out var pct))
                onPercent(pct);
        }

        process.OutputDataReceived += (_, e) => HandleOutputLine(stdoutSb, stdoutClosed, e.Data);
        process.ErrorDataReceived += (_, e) => HandleOutputLine(stderrSb, stderrClosed, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitTask = process.WaitForExitAsync(ct);

        // Leer pipes en paralelo con WaitForExit para evitar deadlock por buffer lleno
        var allDone = Task.WhenAll(waitTask, stdoutClosed.Task, stderrClosed.Task);
        if (timeout.HasValue)
        {
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var delayTask = Task.Delay(timeout.Value, delayCts.Token);
            var completed = await Task.WhenAny(allDone, delayTask).ConfigureAwait(true);
            if (completed != allDone)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                try { await allDone.ConfigureAwait(true); } catch { }
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException($"{stage} cancelado por usuario", ct);
                Log($"⚠️ {stage} timeout ({inputName}): proceso killed tras {timeout.Value.TotalSeconds:0}s");
                return false;
            }
            delayCts.Cancel();
        }
        try
        {
            await allDone.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).ConfigureAwait(true); } catch { }
            throw;
        }

        if (process.ExitCode == 0)
        {
            var elapsedOk = DateTimeOffset.UtcNow - startedAt;
            LogDebug($"✅ {stage} ok ({inputName}) en {elapsedOk.TotalSeconds:F1}s");
            return true;
        }

        var err = stderrSb.ToString().Trim();
        var stdout = stdoutSb.ToString().Trim();
        var msg = !string.IsNullOrWhiteSpace(err) ? err
            : !string.IsNullOrWhiteSpace(stdout) ? stdout
            : $"exit={process.ExitCode}";
        var elapsedFail = DateTimeOffset.UtcNow - startedAt;
        Log($"⚠️ {stage} fallo ({inputName}) tras {elapsedFail.TotalSeconds:F1}s: {msg}");
        return false;
    }

    private static bool TryParsePercentFromText(string text, out int percent)
    {
        percent = -1;
        if (string.IsNullOrWhiteSpace(text)) return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i])) continue;

            int start = i;
            int value = 0;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                value = (value * 10) + (text[i] - '0');
                if (value > 1000) break;
                i++;
            }

            int j = i;
            while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
            if (j < text.Length && text[j] == '%')
            {
                if (value >= 0 && value <= 100)
                {
                    percent = value;
                    return true;
                }
                return false;
            }

            i = Math.Max(i, start);
        }

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

        // Match by DownloadUrl first (precise); fallback to FileName for items enqueued without URL
        var item = _downloadQueue.FirstOrDefault(q =>
            q.SourceFile is not null &&
            string.Equals(q.SourceFile.DownloadUrl, file.DownloadUrl, StringComparison.OrdinalIgnoreCase) &&
            q.Percent < 100.0);
        if (item is null)
            item = _downloadQueue.FirstOrDefault(q =>
                q.SourceFile is not null &&
                string.Equals(q.SourceFile.DownloadUrl, file.DownloadUrl, StringComparison.OrdinalIgnoreCase));
        // Last-resort: filename only (legacy, handles items with no URL)
        if (item is null)
            item = _downloadQueue.FirstOrDefault(q =>
                string.Equals(q.FileName, file.FileName, StringComparison.OrdinalIgnoreCase) && q.Percent < 100.0);
        if (item is null)
            item = _downloadQueue.FirstOrDefault(q =>
                string.Equals(q.FileName, file.FileName, StringComparison.OrdinalIgnoreCase));
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
        // No reasignar lstResults.ItemsSource — el binding a _resultsView.View
        // se establece en el ctor (R8) y debe permanecer activo.
        // _allResults.Clear() dispara CollectionChanged y limpia la vista.
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

    private void VideoLog(string msg)
    {
        // Write only to video-specific log UI
        Dispatcher.InvokeAsync(() =>
        {
            txtVideoLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            txtVideoLog.ScrollToEnd();
        });
    }

    private void BtnClearVideoLog_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            txtVideoLog.Clear();
        });
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
        // Use only stable identity (source+composer+title), NOT file signatures.
        // File signatures change when URLs rotate or new formats are added → tags lost.
        => NormalizeKey(item.Source) + "|" + NormalizeKey(item.Composer) + "|" + NormalizeKey(item.Title);

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

        // Compatibilidad hacia atrás 1: clave con Source+DedupKey(composer+title+firma-archivos)
        var legacyKeyV2 = NormalizeKey(item.Source) + "|" + BuildDedupKey(item);
        if (_savedTags.TryGetValue(legacyKeyV2, out var tagV2))
        {
            item.UserTag = tagV2;
            // Migrar a clave estable
            _savedTags[key] = tagV2;
            return;
        }

        // Compatibilidad hacia atrás 2: clave sin Source (antes de Round 10)
        var legacyKeyV1 = BuildDedupKey(item);
        if (_savedTags.TryGetValue(legacyKeyV1, out var tagV1))
        {
            item.UserTag = tagV1;
            _savedTags[key] = tagV1;
            return;
        }

        item.UserTag = string.Empty;
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
                _audiverisKnownPageFailures.TryAdd(Path.GetFullPath(path), 0);
            }
            catch { }
        }
        _audiverisKnownPageFailuresDirty = false;

        if (_audiverisKnownPageFailures.Count > 0)
            Log($"ℹ️ Audiveris: {_audiverisKnownPageFailures.Count} PAGE fail(s) cargados de sesión previa.");
    }

    private void SaveAudiverisPageFailures()
    {
        if (!_audiverisKnownPageFailuresDirty)
            return;
        JsonStore.Save(_audiverisPageFailuresPath, _audiverisKnownPageFailures.Keys.OrderBy(p => p).ToList());
        _audiverisKnownPageFailuresDirty = false;
    }

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
        _audiverisTimeoutFamiliesDirty = false;
    }

    private void SaveAudiverisTimeoutFamilies()
    {
        if (!_audiverisTimeoutFamiliesDirty)
            return;
        var now = DateTime.UtcNow;
        var snapshot = _audiverisTimeoutFamilies
            .Where(kv => kv.Value > now)
            .Select(kv => new AudiverisTimeoutFamilyEntry { FamilyKey = kv.Key, ExpiresUtc = kv.Value })
            .OrderBy(x => x.FamilyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        JsonStore.Save(_audiverisTimeoutFamiliesPath, snapshot);
        _audiverisTimeoutFamiliesDirty = false;
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

    private sealed class TimeoutTelemetryEntry
    {
        public string FamilyKey { get; set; } = string.Empty;
        public int ComputedTimeoutSeconds { get; set; }
        public double ActualElapsedSeconds { get; set; }
        public bool TimedOut { get; set; }
        public DateTime DateUtc { get; set; }
    }

    /// <summary>Ring-buffer telemetry: appends entry, keeps last <paramref name="maxEntries"/> (default 50). Thread-safe via file lock.</summary>
    private static void AppendTelemetry(string path, TimeoutTelemetryEntry entry, int maxEntries = 50)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            List<TimeoutTelemetryEntry> list = [];
            if (File.Exists(path))
            {
                try { list = JsonSerializer.Deserialize<List<TimeoutTelemetryEntry>>(File.ReadAllText(path)) ?? []; }
                catch { list = []; }
            }
            list.Add(entry);
            if (list.Count > maxEntries) list.RemoveRange(0, list.Count - maxEntries);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }), System.Text.Encoding.UTF8);
            File.Move(tmp, path, overwrite: true);
        }
        catch { }
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
            if (hoursElapsed < 0) hoursElapsed = 0; // guard: clock adjusted backward
            var decayed = (int)Math.Floor(hoursElapsed / decayHours);
            var strikes = Math.Max(0, kv.Value.Strikes - decayed);
            if (strikes <= 0) continue;
            _audiverisTimeoutStrikes[kv.Key] = Math.Clamp(strikes, 1, 12);
            _audiverisStrikeLastUtc[kv.Key] = kv.Value.LastTimeoutUtc;
        }
        _audiverisTimeoutStrikesDirty = false;
    }

    private void SaveAudiverisTimeoutStrikes()
    {
        if (!_audiverisTimeoutStrikesDirty)
            return;
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
        _audiverisTimeoutStrikesDirty = false;
    }

    private bool IsAudiverisFamilyInTimeoutCooldown(string inputPath, string? familyKey = null)
    {
        familyKey ??= NormalizeAudiverisFamilyKey(inputPath);
        // Aplicar strike decay si ha pasado el tiempo
        if (_audiverisStrikeLastUtc.TryGetValue(familyKey, out var lastStrikeUtc))
        {
            var decayHours = _audiverisStrikeDecayHours;
            if (DateTime.UtcNow > lastStrikeUtc.AddHours(decayHours))
            {
                if (_audiverisTimeoutStrikes.TryRemove(familyKey, out _))
                    _audiverisTimeoutStrikesDirty = true;
                if (_audiverisStrikeLastUtc.TryRemove(familyKey, out _))
                    _audiverisTimeoutStrikesDirty = true;
            }
        }
        if (!_audiverisTimeoutFamilies.TryGetValue(familyKey, out var expiresUtc))
            return false;
        if (expiresUtc > DateTime.UtcNow)
            return true;
        if (_audiverisTimeoutFamilies.TryRemove(familyKey, out _))
            _audiverisTimeoutFamiliesDirty = true;
        return false;
    }

    private void MarkAudiverisFamilyTimeout(string inputPath, string? familyKey = null)
    {
        familyKey ??= NormalizeAudiverisFamilyKey(inputPath);
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
        _audiverisTimeoutFamiliesDirty = true;
        _audiverisTimeoutStrikesDirty = true;
    }

    // ── oemer persistence & cooldown ───────────────────────────────────────

    private void LoadOemerPageFailures()
    {
        var entries = JsonStore.Load<List<string>>(_oemerPageFailuresPath, []);
        _oemerKnownPageFailures.Clear();
        foreach (var path in entries)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try { _oemerKnownPageFailures.TryAdd(Path.GetFullPath(path), 0); } catch { }
        }
        _oemerKnownPageFailuresDirty = false;
        if (_oemerKnownPageFailures.Count > 0)
            Log($"ℹ️ oemer: {_oemerKnownPageFailures.Count} fallo(s) permanentes cargados de sesión previa.");
    }

    private void SaveOemerPageFailures()
    {
        if (!_oemerKnownPageFailuresDirty)
            return;
        JsonStore.Save(_oemerPageFailuresPath, _oemerKnownPageFailures.Keys.OrderBy(p => p).ToList());
        _oemerKnownPageFailuresDirty = false;
    }

    private void LoadOemerTimeoutFamilies()
    {
        var now = DateTime.UtcNow;
        var entries = JsonStore.Load<List<AudiverisTimeoutFamilyEntry>>(_oemerTimeoutFamiliesPath, []);
        _oemerTimeoutFamilies.Clear();
        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.FamilyKey) || entry.ExpiresUtc <= now) continue;
            _oemerTimeoutFamilies[entry.FamilyKey] = entry.ExpiresUtc;
        }
        if (_oemerTimeoutFamilies.Count > 0)
            Log($"ℹ️ oemer: {_oemerTimeoutFamilies.Count} familia(s) en cooldown cargadas de sesión previa.");
        _oemerTimeoutFamiliesDirty = false;
    }

    private void SaveOemerTimeoutFamilies()
    {
        if (!_oemerTimeoutFamiliesDirty)
            return;
        var now = DateTime.UtcNow;
        var snapshot = _oemerTimeoutFamilies
            .Where(kv => kv.Value > now)
            .Select(kv => new AudiverisTimeoutFamilyEntry { FamilyKey = kv.Key, ExpiresUtc = kv.Value })
            .OrderBy(x => x.FamilyKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        JsonStore.Save(_oemerTimeoutFamiliesPath, snapshot);
        _oemerTimeoutFamiliesDirty = false;
    }

    private void LoadOemerTimeoutStrikes()
    {
        var decayHours = _oemerStrikeDecayHours;
        var now = DateTime.UtcNow;
        var data = JsonStore.Load<Dictionary<string, AudiverisStrikeRecord>>(_oemerTimeoutStrikesPath, []);
        _oemerTimeoutStrikes.Clear();
        _oemerStrikeLastUtc.Clear();
        foreach (var kv in data)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null || kv.Value.Strikes <= 0) continue;
            var hoursElapsed = (now - kv.Value.LastTimeoutUtc).TotalHours;
            if (hoursElapsed < 0) hoursElapsed = 0; // guard: clock adjusted backward
            var decayed = (int)Math.Floor(hoursElapsed / decayHours);
            var strikes = Math.Max(0, kv.Value.Strikes - decayed);
            if (strikes <= 0) continue;
            _oemerTimeoutStrikes[kv.Key] = Math.Clamp(strikes, 1, 12);
            _oemerStrikeLastUtc[kv.Key] = kv.Value.LastTimeoutUtc;
        }
        _oemerTimeoutStrikesDirty = false;
    }

    private void SaveOemerTimeoutStrikes()
    {
        if (!_oemerTimeoutStrikesDirty)
            return;
        var snapshot = _oemerTimeoutStrikes
            .Where(kv => kv.Value > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => new AudiverisStrikeRecord
                {
                    Strikes = kv.Value,
                    LastTimeoutUtc = _oemerStrikeLastUtc.TryGetValue(kv.Key, out var ts) ? ts : DateTime.UtcNow
                },
                StringComparer.OrdinalIgnoreCase);
        JsonStore.Save(_oemerTimeoutStrikesPath, snapshot);
        _oemerTimeoutStrikesDirty = false;
    }

    private bool IsOemerFamilyInTimeoutCooldown(string inputPath, string? familyKey = null)
    {
        familyKey ??= NormalizeAudiverisFamilyKey(inputPath);
        // Aplicar strike decay si ha pasado el tiempo
        if (_oemerStrikeLastUtc.TryGetValue(familyKey, out var lastStrikeUtc))
        {
            var decayHours = _oemerStrikeDecayHours;
            if (DateTime.UtcNow > lastStrikeUtc.AddHours(decayHours))
            {
                if (_oemerTimeoutStrikes.TryRemove(familyKey, out _))
                    _oemerTimeoutStrikesDirty = true;
                if (_oemerStrikeLastUtc.TryRemove(familyKey, out _))
                    _oemerTimeoutStrikesDirty = true;
            }
        }
        if (!_oemerTimeoutFamilies.TryGetValue(familyKey, out var expiresUtc))
            return false;
        if (expiresUtc > DateTime.UtcNow)
            return true;
        if (_oemerTimeoutFamilies.TryRemove(familyKey, out _))
            _oemerTimeoutFamiliesDirty = true;
        return false;
    }

    private void MarkOemerFamilyTimeout(string inputPath, string? familyKey = null)
    {
        familyKey ??= NormalizeAudiverisFamilyKey(inputPath);
        var now = DateTime.UtcNow;
        var newStrikes = _oemerTimeoutStrikes.AddOrUpdate(
            familyKey, 1, (_, current) => Math.Clamp(current + 1, 1, 12));
        _oemerStrikeLastUtc[familyKey] = now;
        var multiplier = Math.Min(Math.Pow(2, newStrikes - 1), 1024.0);
        var cooldownMinutes = (long)Math.Min(_oemerTimeoutCooldownMinutes * multiplier, 10080.0);
        var expiresUtc = now.AddMinutes(cooldownMinutes);
        _oemerTimeoutFamilies.AddOrUpdate(familyKey, expiresUtc, (_, current) => current > expiresUtc ? current : expiresUtc);
        _oemerTimeoutFamiliesDirty = true;
        _oemerTimeoutStrikesDirty = true;
    }

    /// <summary>Filtra <paramref name="files"/> omitiendo los que están en cooldown según <paramref name="isCooldown"/>.
    /// Devuelve los archivos no bloqueados. <paramref name="skippedCount"/> = total archivos omitidos;
    /// <paramref name="skippedFamilyKeys"/> = claves de familia únicas omitidas.</summary>
    private List<string> FilterByCooldown(
        IEnumerable<string> files,
        Func<string, string?, bool> isCooldown,
        out int skippedCount,
        out HashSet<string> skippedFamilyKeys)
    {
        skippedCount = 0;
        skippedFamilyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var file in files)
        {
            var fk = NormalizeAudiverisFamilyKey(file);
            if (isCooldown(file, fk))
            {
                skippedCount++;
                skippedFamilyKeys.Add(fk);
            }
            else
            {
                result.Add(file);
            }
        }
        return result;
    }

    /// <summary>Devuelve un sufijo legible con las top <paramref name="top"/> familias en cooldown y su tiempo restante, o string.Empty si ninguna aplica.</summary>
    private static string FormatTopCooldownFamilies(IEnumerable<string> familyKeys, ConcurrentDictionary<string, DateTime> cooldownDict, int top = 5)
    {
        var now = DateTime.UtcNow;
        var sorted = familyKeys
            .Select(k => cooldownDict.TryGetValue(k, out var exp) ? (Key: k, Remaining: exp - now) : (Key: k, Remaining: TimeSpan.Zero))
            .Where(x => x.Remaining > TimeSpan.Zero)
            .OrderByDescending(x => x.Remaining)
            .Take(top)
            .ToList();
        if (sorted.Count == 0) return string.Empty;
        return " [" + string.Join(", ", sorted.Select(x => $"{Path.GetFileName(x.Key)}:{(int)x.Remaining.TotalMinutes}min")) + "]";
    }

    private void UpdateOemerStatus()
    {
        if (!IsInitialized || txtOemerStatus is null) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateOemerStatus, DispatcherPriority.Background);
            return;
        }
        var now = DateTime.UtcNow;
        var activeFamilies = _oemerTimeoutFamilies
            .Where(kv => kv.Value > now)
            .OrderBy(kv => kv.Value)
            .ToList();
        var strikeCount = _oemerTimeoutStrikes.Count(kv => kv.Value > 0);
        var cooldownCount = activeFamilies.Count;
        txtOemerStatus.Text = cooldownCount > 0 || strikeCount > 0
            ? $"Cooldown: {cooldownCount} | Strikes: {strikeCount}"
            : string.Empty;
        if (cooldownCount > 0)
        {
            var lines = activeFamilies.Take(10).Select(kv =>
            {
                var key = kv.Key.Length > 60 ? kv.Key[^60..] : kv.Key;
                var remaining = (kv.Value - now).TotalMinutes;
                var strikes = _oemerTimeoutStrikes.TryGetValue(kv.Key, out var s) ? s : 0;
                return $"{key}  [{remaining:F0}min, s{strikes}]";
            });
            txtOemerStatus.ToolTip = "Familias en cooldown:\n" + string.Join("\n", lines) +
                (cooldownCount > 10 ? $"\n... +{cooldownCount - 10} más" : string.Empty);
        }
        else { txtOemerStatus.ToolTip = null; }
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
        // EnableCpdl removido
        if (state.EnableMusopen.HasValue)
        {
            _enableMusopen = state.EnableMusopen.Value;
            chkEnableMusopen.IsChecked = _enableMusopen;
        }
        if (state.OnlyClassical.HasValue)
        {
            _onlyClassical = state.OnlyClassical.Value;
            chkOnlyClassical.IsChecked = _onlyClassical;
        }
        // Restaurar sesión Musopen persistida
        if (!string.IsNullOrWhiteSpace(state.MusopenCookieHeader))
        {
            _musopen.SetSession(state.MusopenCookieHeader, state.MusopenUserAgent);
            Log("🔐 Musopen sesión restaurada desde estado guardado.");
        }
        // btnCpdlSession.IsEnabled = false;  // CPDL removido - botón removido del XAML
        btnMusopenSession.IsEnabled = _enableMusopen;
        if (state.AutoConvertAudiveris.HasValue)
            chkAutoConvertAudiveris.IsChecked = state.AutoConvertAudiveris.Value;
        if (state.AutoConvertOemer.HasValue)
            chkAutoConvertOemer.IsChecked = state.AutoConvertOemer.Value;
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
            // EnableCpdl removido
            EnableMusopen = _enableMusopen,
            MusopenCookieHeader = muCookie,
            MusopenUserAgent = muUA,
            AutoConvertAudiveris = chkAutoConvertAudiveris.IsChecked == true,
            AutoConvertOemer = chkAutoConvertOemer.IsChecked == true,
            OnlyClassical = chkOnlyClassical.IsChecked == true
        });
    }

    private static readonly string[] NonClassicalHints =
    [
        "jazz", "blues", "ragtime", "swing", "bossa", "salsa", "tango",
        "rock", "metal", "punk", "pop", "hip hop", "rap", "electronic",
        "edm", "techno", "house", "disco", "funk", "country", "flamenco"
    ];

    private static bool IsClassicalItem(PartituraItem item)
    {
        var composer = item.Composer ?? string.Empty;
        var title = item.Title ?? string.Empty;
        var haystack = (composer + " " + title).Trim();

        if (string.IsNullOrWhiteSpace(haystack))
            return false;

        // Hard reject on explicit non-classical genre hints.
        if (NonClassicalHints.Any(h => haystack.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Strong positive signal by known classical composers.
        if (KnownComposers.Any(c => composer.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Source-level fallback for repositories already focused on classical repertoire.
        return item.Source is "Mutopia" or "OpenScore";
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
            Interlocked.Increment(ref _cacheHits);
            // Indexer write is atomic on ConcurrentDictionary; avoids TryUpdate race
            // where another thread updates LastAccessed between TryGetValue and TryUpdate.
            _searchCache[key] = (entry.Expires, now, entry.Results);
            UpdateCacheStats();
            progress?.Report($"⚡ Cache {source}: {entry.Results.Count} obras");
            return CloneItems(entry.Results);
        }

        Interlocked.Increment(ref _cacheMisses);
        UpdateCacheStats();
        var fresh = await fetch(progress, ct);
        // Cache stores its own clone; caller receives the original (fresh is newly created by fetch,
        // no other reference exists). This halves allocations vs CloneItems(fresh) twice.
        _searchCache[key] = (Expires: now.Add(SearchCacheTtl), LastAccessed: now, Results: CloneItems(fresh));

        // LRU eviction: keep only MaxCacheEntries newest-accessed (best-effort; slight race is acceptable)
        if (_searchCache.Count > MaxCacheEntries)
        {
            var oldest = _searchCache.MinBy(kv => kv.Value.LastAccessed).Key;
            _searchCache.TryRemove(oldest, out _);
        }

        return fresh;
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
