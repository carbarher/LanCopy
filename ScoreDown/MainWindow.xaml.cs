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
    private readonly ImslpService _imslp = new();
    private readonly MutopiaService _mutopia = new();
    private readonly CpdlService _cpdl = new();
    private readonly DownloadService _downloader = new();

    private readonly ObservableCollection<PartituraItem> _allResults = new();
    private readonly ObservableCollection<DownloadQueueItem> _downloadQueue = [];
    private readonly ObservableCollection<DownloadHistoryItem> _downloadHistory = [];
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
    private readonly HashSet<string> _savedFavorites = new(StringComparer.OrdinalIgnoreCase);
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
    private readonly string _favoritesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "favorites.json");
    private readonly string _downloadHistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "download-history.json");
    private readonly string _offlineLibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreDown", "offline-library.json");
    private int _cacheHits;
    private int _cacheMisses;
    private int _sessionSearches;
    private int _sessionResults;
    private int _sessionDownloads;
    private long _sessionBytes;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _catalogCts;
    // Dedup set para catálogo — O(1) lookup vs O(N²) .Any()
    private readonly HashSet<string> _catalogSeenKeys = new(StringComparer.OrdinalIgnoreCase);
    // Cache de compositores conocidos en resultados actuales — evita iterar _allResults por tecla
    private readonly HashSet<string> _knownComposersCache = new(StringComparer.OrdinalIgnoreCase);

    private PartituraItem? _contextItem;  // item bajo clic derecho
    private bool _suppressTagUiEvents;
    private readonly HashSet<string> _pausedQueueFiles = new(StringComparer.OrdinalIgnoreCase);

    // Production features: file logging + circuit breaker
    private FileLoggingService? _fileLogger;
    private GlobalCircuitBreaker? _circuitBreaker;
    private int _cleanupDone;
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

        _currentDestFolder = txtDestFolder.Text?.Trim() ?? string.Empty;
        LoadSearchHistory();
        LoadTags();
        LoadFavorites();
        LoadDownloadHistory();
        LoadOfflineLibrary();
        RefreshHistoryCombo();
        UpdateCacheStats();
        lstQueue.ItemsSource = _downloadQueue;
        lstDownloadHistory.ItemsSource = _downloadHistory;
        UpdateSessionStats();
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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        CleanupRuntimeResources();
        SaveTags();
        SaveFavorites();
        SaveDownloadHistory();
        SaveOfflineLibrary();

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

        // _knownComposersCache se puebla en DoSearchAsync tras cada búsqueda (O(1) lookup aquí)
        var suggestions = KnownComposers
            .Concat(_knownComposersCache)
            .Concat(_savedTags.Keys.Select(ParseComposerFromSavedKey))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
            _suppressTagUiEvents = true;
            txtTagEditor.Text = string.Empty;
            btnFavorite.IsChecked = false;
            btnFavorite.Content = "☆ Favorito";
            _suppressTagUiEvents = false;
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
        _suppressTagUiEvents = true;
        txtTagEditor.Text = item.UserTag;
        btnFavorite.IsChecked = item.IsFavorite;
        btnFavorite.Content = item.IsFavorite ? "★ Favorito" : "☆ Favorito";
        _suppressTagUiEvents = false;
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
        var key = BuildDedupKey(item);
        if (string.IsNullOrWhiteSpace(item.UserTag))
            _savedTags.Remove(key);
        else
            _savedTags[key] = item.UserTag;

        SaveTags();
        txtTagStatus.Text = string.IsNullOrWhiteSpace(item.UserTag) ? "Tag limpiado" : "Tag guardado";
        lstResults.Items.Refresh();
    }

    private void BtnClearTag_Click(object sender, RoutedEventArgs e)
    {
        txtTagEditor.Text = string.Empty;
        BtnSaveTag_Click(sender, e);
    }

    private void BtnFavorite_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressTagUiEvents) return;
        if (lstResults.SelectedItem is not PartituraItem item) return;

        item.IsFavorite = btnFavorite.IsChecked == true;
        btnFavorite.Content = item.IsFavorite ? "★ Favorito" : "☆ Favorito";
        var key = BuildDedupKey(item);
        if (item.IsFavorite) _savedFavorites.Add(key); else _savedFavorites.Remove(key);
        SaveFavorites();
        txtTagStatus.Text = item.IsFavorite ? "Favorito guardado" : "Favorito quitado";
        lstResults.Items.Refresh();
        ApplyFilter();
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

            var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress, file => _pausedQueueFiles.Contains(file.DownloadUrl));
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
        _pausedQueueFiles.Add(queueItem.SourceFile.DownloadUrl);
        queueItem.Status = "Pausado";
        lstQueue.Items.Refresh();
    }

    private async void CtxQueueResume_Click(object sender, RoutedEventArgs e)
    {
        if (lstQueue.SelectedItem is not DownloadQueueItem queueItem || queueItem.SourceFile is null || queueItem.SourceItem is null) return;
        _pausedQueueFiles.Remove(queueItem.SourceFile.DownloadUrl);
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

        var result = await _downloader.DownloadFileAsync(queueItem.SourceFile, subFolder, progress);
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

        _ = DoFetchCatalogAsync(forceAllSources: true, includeImlspWhenAll: true);
    }

    private void BtnCancelCatalog_Click(object sender, RoutedEventArgs e) => _catalogCts?.Cancel();

    /// <summary>
    /// Fase 1: cataloga metadatos de la fuente seleccionada (sin añadir a _allResults para evitar
    ///         O(N²) y freezes de UI con 150k+ items).
    /// Fase 2: descarga automáticamente todos los archivos, saltando los ya existentes en disco.
    ///         IMSLP: solo descarga si se seleccionó explícitamente (cada obra requiere una petición
    ///         adicional para obtener los enlaces; a 150k obras = horas). Se avisa al usuario.
    ///         Mutopia / CPDL: descarga completa automática.
    /// </summary>
    private async Task DoFetchCatalogAsync(bool forceAllSources = false, bool includeImlspWhenAll = false)
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

        var source = (cmbSource.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "IMSLP";
        if (forceAllSources)
            source = "Todas";
        if (source == "Offline")
        {
            Log("⚠️ Selecciona IMSLP, Mutopia, CPDL o Todas para descargar catálogo");
            return;
        }

        _catalogCts?.Cancel();
        _catalogCts = new CancellationTokenSource();
        var ct = _catalogCts.Token;

        btnFetchCatalog.IsEnabled = false;
        btnDownloadAll.IsEnabled = false;
        btnCancelCatalog.IsEnabled = true;
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

            var fetchTasks = new List<Task>();
            if (source is "Mutopia" or "Todas")
                fetchTasks.Add(_mutopia.FetchAllAsync(OnItem, progress, ct));
            if (source is "CPDL" or "Todas")
                fetchTasks.Add(_cpdl.FetchAllAsync(OnItem, progress, ct));
            if (source is "IMSLP" or "Todas")
                fetchTasks.Add(_imslp.FetchAllAsync(OnItem, progress, ct));

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
            // IMSLP requiere una petición HTTP por obra para obtener links de descarga.
            // Con 150k+ obras eso son horas. Solo descargamos si fue seleccionada explícitamente
            // Y se excluye de descarga automática en modo "Todas".
            var downloadable = source == "Todas" && !includeImlspWhenAll
                ? catalogItems.Where(i => i.Source != "IMSLP").ToList()
                : catalogItems.ToList();

            int imslpCount = 0;
            if (source == "Todas" && !includeImlspWhenAll)
            {
                imslpCount = catalogItems.Count(i => i.Source == "IMSLP");
                Log($"ℹ️ IMSLP: {imslpCount} obras catalogadas sin descarga automática (usa búsqueda manual)");
            }
            else if (source == "Todas" && includeImlspWhenAll)
            {
                Log("ℹ️ Modo Descargar todo: IMSLP incluido en descarga automática (puede tardar bastante)");
            }

            int totalDone = 0;
            int consecutiveErrors = 0;
            // Concurrencia adaptativa: SemaphoreSlim permite ajustar MaxCount en runtime
            // empieza en 3, baja a 1 si ≥5 errores seguidos
            var concSem = new SemaphoreSlim(3, 3);
            int currentConcurrency = 3;

            Log($"📥 Fase 2: descargando {downloadable.Count} obras ({currentConcurrency} simultáneas)...");

            var downloadTasks = downloadable.Select(async item =>
            {
                await concSem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    ct.ThrowIfCancellationRequested();

                    // IMSLP/CPDL: cargar links de descarga (petición por obra)
                    // Con reintentos — transient failures (429, 502-504) son esperables en catálogo masivo
                    if (item.Files.Count == 0)
                    {
                        bool loaded = false;
                        int loadAttempts = 0;
                        const int maxLoadAttempts = 2;  // 1 reintento

                        while (loadAttempts < maxLoadAttempts && !loaded)
                        {
                            try
                            {
                                loaded = item.Source == "IMSLP"
                                    ? await _imslp.LoadFilesAsync(item, ct)
                                    : await _cpdl.LoadFilesAsync(item, ct);
                                if (loaded)
                                    System.Threading.Volatile.Write(ref consecutiveErrors, 0);
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { /* transient error, reintentará */ }

                            if (!loaded)
                            {
                                loadAttempts++;
                                if (loadAttempts < maxLoadAttempts)
                                {
                                    // Backoff: 2s + jitter
                                    var backoffMs = 2000 + Random.Shared.Next(0, 500);
                                    await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                                }
                            }
                        }

                        if (!loaded)
                        {
                            System.Threading.Interlocked.Increment(ref consecutiveErrors);
                            return;
                        }
                    }

                    var subFolder = BuildDestSubFolder(destFolder!, item);

                    foreach (var file in item.Files)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            await _downloader.DownloadFileAsync(file, subFolder, null, null, ct);
                            System.Threading.Volatile.Write(ref consecutiveErrors, 0);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch
                        {
                            // Reducir concurrencia si acumulamos errores seguidos (posible 429)
                            var errors = System.Threading.Interlocked.Increment(ref consecutiveErrors);
                            if (errors >= 5)
                            {
                                // Solo intenta bajar a concurrencia 1 una vez (con CompareExchange)
                                if (System.Threading.Interlocked.CompareExchange(ref currentConcurrency, 1, 3) == 3)
                                {
                                    // Drena 2 slots del semáforo (3 -> 1)
                                    // Pero lo hace con try/catch para evitar race conditions
                                    try
                                    {
                                        await concSem.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                                        await concSem.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                        // Si falla el drenaje, continuamos de todas formas
                                        // (el error de concurrencia es menos crítico que el timeout)
                                    }
                                    progress.Report("⚠️ Errores repetidos: reduciendo a 1 descarga simultánea");
                                    await Task.Delay(3000, ct).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    var done = System.Threading.Interlocked.Increment(ref totalDone);
                    if (done % 50 == 0)
                        progress.Report($"📥 {done} obras descargadas...");
                }
                finally
                {
                    concSem.Release();
                }
            }).ToList();

            await Task.WhenAll(downloadTasks).ConfigureAwait(false);

            progress.Report($"✅ Completado: {totalDone} obras descargadas");
            ShowToast("ScoreDown", $"Catálogo completo: {totalDone} obras descargadas");
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
            btnFetchCatalog.IsEnabled = true;
            btnDownloadAll.IsEnabled = true;
            btnCancelCatalog.IsEnabled = false;
        }
    }

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
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        SetSearchRunning(true);
        ClearResults();
        Log($"🔍 Buscando: {query}");

        try
        {
            IProgress<string> progress = new Progress<string>(msg => { txtStatus.Text = msg; Log(msg); });
            var source = (cmbSource.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "IMSLP";

            PurgeExpiredCache();  // Limpia cache expirado antes de nueva búsqueda

            List<PartituraItem> results = [];

            if (source == "IMSLP")
            {
                results = await FetchWithCacheAsync("IMSLP", query,
                    (p, token) => _imslp.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "Mutopia")
            {
                results = await FetchWithCacheAsync("Mutopia", query,
                    (p, token) => _mutopia.SearchAsync(query, p, token), progress, ct);
            }
            else if (source == "CPDL")
            {
                results = await FetchWithCacheAsync("CPDL", query,
                    (p, token) => _cpdl.SearchAsync(query, p, token), progress, ct);
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

                progress.Report("🔍 Buscando en IMSLP...");
                var imslpTask = SafeSearchAsync("IMSLP", () =>
                    FetchWithCacheAsync("IMSLP", query, (p, token) => _imslp.SearchAsync(query, p, token), null, ct));
                progress.Report("🔍 Buscando en Mutopia Project...");
                var mutopiaTask = SafeSearchAsync("Mutopia", () =>
                    FetchWithCacheAsync("Mutopia", query, (p, token) => _mutopia.SearchAsync(query, p, token), null, ct));
                progress.Report("🔍 Buscando en CPDL...");
                var cpdlTask = SafeSearchAsync("CPDL", () =>
                    FetchWithCacheAsync("CPDL", query, (p, token) => _cpdl.SearchAsync(query, p, token), null, ct));

                var all = await Task.WhenAll(imslpTask, mutopiaTask, cpdlTask);
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
                    .ThenByDescending(i => string.Equals(i.Source, "IMSLP", StringComparison.OrdinalIgnoreCase))
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
        bool onlyFavorites = chkOnlyFavorites.IsChecked == true;
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
            if (onlyFavorites && !item.IsFavorite) return false;
            if (!string.IsNullOrWhiteSpace(tagFilter) && !item.UserTag.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(titleQuery) && !item.Title.Contains(titleQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(composerQuery) && !item.Composer.Contains(composerQuery, StringComparison.OrdinalIgnoreCase)) return false;
            if (onlyPdf && !item.Files.Any(f => f.Format == "PDF")) return false;
            if (onlyMidi && !item.Files.Any(f => f.Format == "MIDI")) return false;
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
            var key = BuildDedupKey(item);
            if (string.IsNullOrWhiteSpace(newTag)) _savedTags.Remove(key); else _savedTags[key] = newTag;
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

        var jobs = _filtered
            .Where(i => i.IsSelected)
            .SelectMany(i => i.Files
                .Where(f => (!onlyPdf || f.Format == "PDF") && (!onlyMidi || f.Format == "MIDI"))
                .Select(f => (item: i, file: f)))
            .ToList();

        if (jobs.Count == 0)
        {
            DarkDialogService.ShowMessage(this, "No hay archivos que descargar con los filtros actuales.", "Sin archivos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _downloadCts?.Cancel();
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
                else Log(r.message);
            });

            var fileProgress = new Progress<(PartituraFile file, string state, double filePct, double speedKBps)>(r =>
            {
                txtFileProgress.Text = $"↳ {r.file.FileName} · {r.speedKBps:F0} KB/s";
                pbFileProgress.Value = r.filePct;
                UpdateQueue(r.file, r.state, r.filePct, r.speedKBps);
            });

            var result = await _downloader.DownloadAllAsync(jobs, destFolder, progress, fileProgress, file => _pausedQueueFiles.Contains(file.DownloadUrl), ct);
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
                Status = _pausedQueueFiles.Contains(job.file.DownloadUrl) ? $"Pausado · {job.item.DisplayName}" : $"Pendiente · {job.item.DisplayName}",
                Percent = 0
            });
        }
        lstQueue.Items.Refresh();
    }

    private void UpdateQueue(PartituraFile file, string state, double percent, double speedKBps)
    {
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

        lstQueue.Items.Refresh();
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
        var key = BuildDedupKey(item);
        item.UserTag = _savedTags.TryGetValue(key, out var tag) ? tag : string.Empty;
        item.IsFavorite = _savedFavorites.Contains(key);
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
        => _offlineLibraryItems = JsonStore.Load<List<PartituraItem>>(_offlineLibraryPath, []);

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

    private void LoadFavorites()
    {
        var items = JsonStore.Load<List<string>>(_favoritesPath, []);
        _savedFavorites.Clear();
        foreach (var item in items) _savedFavorites.Add(item);
    }

    private void SaveFavorites()
        => JsonStore.Save(_favoritesPath, _savedFavorites.OrderBy(x => x).ToList());

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
            Favorites = _savedFavorites.OrderBy(x => x).ToList(),
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
            _savedFavorites.Clear();
            foreach (var key in data.Favorites)
                _savedFavorites.Add(key);
            _offlineLibraryItems = CloneItems(data.Items);

            SaveTags();
            SaveFavorites();
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
    }

    private void SaveUiState()
        => JsonStore.Save(_uiStatePath, new UiState
        {
            DestinationFolder = txtDestFolder.Text,
            Source = (cmbSource.SelectedItem as ComboBoxItem)?.Content?.ToString(),
            FilterSource = (cmbFilterSource.SelectedItem as ComboBoxItem)?.Content?.ToString()
        });

    private static void SelectComboItemByText(System.Windows.Controls.ComboBox combo, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (string.Equals(text, "Ambas", StringComparison.OrdinalIgnoreCase))
            text = "Todas";
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
            UserTag = i.UserTag,
            IsFavorite = i.IsFavorite,
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

}
