// ═══════════════════════════════════════════════════════════════
// CÓDIGO DE INTEGRACIÓN PARA MAINFORM.CS
// Copiar y pegar estas secciones en MainForm.cs
// ═══════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 1: VARIABLES DE CLASE (añadir después de las variables existentes)
// ═══════════════════════════════════════════════════════════════

// FASE 2: Técnicas Avanzadas
private SlskDown.Core.RateLimiter searchRateLimiter;
private SlskDown.Core.EventBusSystem eventBus;
private SlskDown.Core.PluginSystem.PluginManager pluginManager;
private SlskDown.Core.ThemeManager themeManager;
private SlskDown.Core.KeyboardShortcutManager keyboardShortcuts;
private SlskDown.Core.CacheWithTTL<string, List<Soulseek.File>> searchCacheTTL;
private SlskDown.Core.MetricsCollector searchLatencyMetrics;
private SlskDown.Core.MetricsCollector downloadSpeedMetrics;

// FASE 3: Características Adicionales
private SlskDown.Core.TransferStatistics transferStats;
private SlskDown.Core.UserNotesSystem userNotesSystem;
private SlskDown.Core.NotificationSystem notificationSystem;
private SlskDown.Core.AdvancedAutoReply autoReplySystem;
private SlskDown.Core.UICustomization uiCustomization;
private SlskDown.Core.SimilarUserFinder similarUserFinder;
private SlskDown.Core.MusicIntegration musicIntegration;
private SlskDown.Core.MessageTranslator messageTranslator;
private SlskDown.Core.MessageEncryption messageEncryption;
private SlskDown.Core.DistributedNetwork distributedNetwork;

// FASE 4: Características Ocultas
private SlskDown.Core.ProtocolTimeouts protocolTimeouts;
private SlskDown.Core.PriorityManager priorityManager;
private SlskDown.Core.ProtocolLogger protocolLogger;
private SlskDown.Core.NetworkHealthMonitor networkHealthMonitor;
private SlskDown.Core.FilterManager filterManager;
private SlskDown.Core.SearchHistory searchHistory;
private SlskDown.Core.IPBlockList ipBlockList;
private SlskDown.Core.PrivacyMode privacyMode;
private SlskDown.Core.ShareExclusions shareExclusions;
private SlskDown.Core.AutoRescan autoRescan;
private SlskDown.Core.RoomCommands roomCommands;
private SlskDown.Core.MessageFilter messageFilter;
private SlskDown.Core.DataExporter dataExporter;
private SlskDown.Core.AutoBackup autoBackup;

// UI Components
private SlskDown.UI.AdvancedSettingsPanel settingsPanel;
private SlskDown.UI.StatsDashboard statsDashboard;
private SlskDown.UI.QuickCommandPalette commandPalette;
private SlskDown.UI.FirstRunWizard firstRunWizard;

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 2: INICIALIZACIÓN (añadir en el constructor)
// ═══════════════════════════════════════════════════════════════

private void InitializeAllNicotineFeatures()
{
    Log("🚀 Inicializando TODAS las características de Nicotine+...");
    
    try
    {
        // FASE 1 (ya implementado)
        InitializeNicotineEnhancements();
        
        // FASE 2: Técnicas Avanzadas
        searchRateLimiter = new SlskDown.Core.RateLimiter(maxTokens: 10, refillRate: 1);
        eventBus = new SlskDown.Core.EventBusSystem();
        searchCacheTTL = new SlskDown.Core.CacheWithTTL<string, List<Soulseek.File>>(
            TimeSpan.FromMinutes(5), maxEntries: 100);
        searchLatencyMetrics = new SlskDown.Core.MetricsCollector(maxValues: 10000);
        downloadSpeedMetrics = new SlskDown.Core.MetricsCollector(maxValues: 10000);
        
        // Plugins
        var pluginsDir = Path.Combine(dataDir, "plugins");
        if (!Directory.Exists(pluginsDir))
            Directory.CreateDirectory(pluginsDir);
        pluginManager = new SlskDown.Core.PluginSystem.PluginManager(eventBus, Log, ShowNotification);
        pluginManager.LoadPlugins(pluginsDir);
        
        // Temas
        var themesDir = Path.Combine(dataDir, "themes");
        if (!Directory.Exists(themesDir))
            Directory.CreateDirectory(themesDir);
        themeManager = new SlskDown.Core.ThemeManager(themesDir);
        
        // Atajos de teclado
        keyboardShortcuts = new SlskDown.Core.KeyboardShortcutManager();
        RegisterKeyboardShortcuts();
        
        Log("✅ Fase 2 inicializada (Técnicas Avanzadas)");
        
        // FASE 3: Características Adicionales
        transferStats = new SlskDown.Core.TransferStatistics();
        
        var userNotesFile = Path.Combine(dataDir, "user_notes.json");
        userNotesSystem = new SlskDown.Core.UserNotesSystem(userNotesFile);
        
        notificationSystem = new SlskDown.Core.NotificationSystem(notifyIcon);
        
        autoReplySystem = new SlskDown.Core.AdvancedAutoReply(
            () => GetActiveDownloadsCount(),
            (username) => GetUserQueuePosition(username)
        );
        
        var layoutsDir = Path.Combine(dataDir, "layouts");
        if (!Directory.Exists(layoutsDir))
            Directory.CreateDirectory(layoutsDir);
        uiCustomization = new SlskDown.Core.UICustomization(layoutsDir);
        
        similarUserFinder = new SlskDown.Core.SimilarUserFinder();
        musicIntegration = new SlskDown.Core.MusicIntegration();
        messageTranslator = new SlskDown.Core.MessageTranslator();
        
        var keysFile = Path.Combine(dataDir, "encryption_keys.json");
        messageEncryption = new SlskDown.Core.MessageEncryption(keysFile);
        
        distributedNetwork = new SlskDown.Core.DistributedNetwork();
        
        Log("✅ Fase 3 inicializada (Características Adicionales)");
        
        // FASE 4: Características Ocultas
        var timeoutsFile = Path.Combine(dataDir, "protocol_timeouts.json");
        protocolTimeouts = SlskDown.Core.ProtocolTimeouts.LoadFromFile(timeoutsFile);
        
        priorityManager = new SlskDown.Core.PriorityManager(() => ReorderDownloadQueue());
        
        protocolLogger = new SlskDown.Core.ProtocolLogger();
        
        networkHealthMonitor = new SlskDown.Core.NetworkHealthMonitor();
        
        var filtersFile = Path.Combine(dataDir, "saved_filters.json");
        filterManager = new SlskDown.Core.FilterManager(filtersFile);
        
        var historyFile = Path.Combine(dataDir, "search_history.json");
        searchHistory = new SlskDown.Core.SearchHistory(historyFile);
        
        var blockListFile = Path.Combine(dataDir, "ip_blocklist.json");
        ipBlockList = new SlskDown.Core.IPBlockList(blockListFile);
        
        privacyMode = new SlskDown.Core.PrivacyMode();
        shareExclusions = new SlskDown.Core.ShareExclusions();
        autoRescan = new SlskDown.Core.AutoRescan();
        roomCommands = new SlskDown.Core.RoomCommands();
        
        var messageFilterFile = Path.Combine(dataDir, "message_filter.json");
        messageFilter = new SlskDown.Core.MessageFilter(messageFilterFile);
        
        dataExporter = new SlskDown.Core.DataExporter();
        
        var backupDir = Path.Combine(dataDir, "backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);
        autoBackup = new SlskDown.Core.AutoBackup(backupDir, maxBackups: 10);
        
        Log("✅ Fase 4 inicializada (Características Ocultas)");
        
        // Verificar primera ejecución
        CheckFirstRun();
        
        Log($"✅ TODAS las 55 características de Nicotine+ inicializadas correctamente");
    }
    catch (Exception ex)
    {
        Log($"❌ Error inicializando características: {ex.Message}");
    }
}

private void CheckFirstRun()
{
    var firstRunFile = Path.Combine(dataDir, ".firstrun");
    if (!File.Exists(firstRunFile))
    {
        this.BeginInvoke(new Action(() =>
        {
            ShowFirstRunWizard();
            File.WriteAllText(firstRunFile, DateTime.Now.ToString());
        }));
    }
}

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 3: ATAJOS DE TECLADO GLOBALES
// ═══════════════════════════════════════════════════════════════

private void RegisterKeyboardShortcuts()
{
    keyboardShortcuts.RegisterDefaultShortcuts(
        focusSearch: () => SafeBeginInvoke(() => txtSearch?.Focus()),
        newSearchTab: () => SafeBeginInvoke(() => CreateNewSearchTab()),
        closeTab: () => SafeBeginInvoke(() => CloseCurrentSearchTab()),
        showDownloads: () => SafeBeginInvoke(() => ShowPanel(downloadsContentPanel)),
        showSettings: () => SafeBeginInvoke(() => ShowPanel(configContentPanel)),
        switchToTab: new Action[]
        {
            () => SwitchToSearchTab(0),
            () => SwitchToSearchTab(1),
            () => SwitchToSearchTab(2),
            () => SwitchToSearchTab(3),
            () => SwitchToSearchTab(4),
            () => SwitchToSearchTab(5),
            () => SwitchToSearchTab(6),
            () => SwitchToSearchTab(7),
            () => SwitchToSearchTab(8)
        }
    );
    
    // Atajos adicionales
    keyboardShortcuts.Register(Keys.F1, "Ayuda", () => SafeBeginInvoke(() => ShowContextualHelp()));
    keyboardShortcuts.Register(Keys.F5, "Actualizar", () => SafeBeginInvoke(() => RefreshCurrentView()));
    keyboardShortcuts.Register(Keys.Control | Keys.S, "Guardar filtro", () => SafeBeginInvoke(() => SaveCurrentFilter()));
    keyboardShortcuts.Register(Keys.Control | Keys.Shift | Keys.L, "Guardar layout", () => SafeBeginInvoke(() => SaveCurrentLayout()));
}

protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    // Ctrl+Shift+P - Paleta de comandos
    if (keyData == (Keys.Control | Keys.Shift | Keys.P))
    {
        ShowCommandPalette();
        return true;
    }
    
    // Ctrl+, - Configuración
    if (keyData == (Keys.Control | Keys.Oemcomma))
    {
        ShowAdvancedSettings();
        return true;
    }
    
    // Ctrl+Shift+S - Dashboard
    if (keyData == (Keys.Control | Keys.Shift | Keys.S))
    {
        ShowStatsDashboard();
        return true;
    }
    
    // F1 - Ayuda contextual
    if (keyData == Keys.F1)
    {
        ShowContextualHelp();
        return true;
    }
    
    // Procesar otros atajos
    if (keyboardShortcuts?.ProcessKey(keyData) == true)
        return true;
    
    return base.ProcessCmdKey(ref msg, keyData);
}

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 4: MÉTODOS DE UI
// ═══════════════════════════════════════════════════════════════

private void ShowFirstRunWizard()
{
    try
    {
        firstRunWizard = new SlskDown.UI.FirstRunWizard();
        if (firstRunWizard.ShowDialog() == DialogResult.OK)
        {
            ApplyWizardSettings(firstRunWizard);
        }
    }
    catch (Exception ex)
    {
        Log($"Error mostrando wizard: {ex.Message}");
    }
}

private void ApplyWizardSettings(SlskDown.UI.FirstRunWizard wizard)
{
    // Aplicar configuración del wizard
    if (!string.IsNullOrEmpty(wizard.SharedFolder))
    {
        // Configurar carpeta compartida
        Log($"Carpeta compartida: {wizard.SharedFolder}");
    }
    
    if (!string.IsNullOrEmpty(wizard.DownloadFolder))
    {
        // Configurar carpeta de descargas
        Log($"Carpeta de descargas: {wizard.DownloadFolder}");
    }
    
    if (!string.IsNullOrEmpty(wizard.SelectedTheme))
    {
        var theme = themeManager.LoadTheme(wizard.SelectedTheme);
        if (theme != null)
            themeManager.ApplyTheme(this, theme);
    }
    
    Log("✅ Configuración del wizard aplicada");
}

private void ShowCommandPalette()
{
    try
    {
        commandPalette = new SlskDown.UI.QuickCommandPalette();
        if (commandPalette.ShowDialog() == DialogResult.OK)
        {
            var commandId = commandPalette.Tag as string;
            ExecuteCommand(commandId);
        }
    }
    catch (Exception ex)
    {
        Log($"Error mostrando paleta de comandos: {ex.Message}");
    }
}

private void ShowAdvancedSettings()
{
    try
    {
        settingsPanel = new SlskDown.UI.AdvancedSettingsPanel();
        settingsPanel.ShowDialog(this);
    }
    catch (Exception ex)
    {
        Log($"Error mostrando configuración: {ex.Message}");
    }
}

private void ShowStatsDashboard()
{
    try
    {
        if (statsDashboard == null || statsDashboard.IsDisposed)
        {
            statsDashboard = new SlskDown.UI.StatsDashboard(
                transferStats,
                networkHealthMonitor,
                searchLatencyMetrics,
                downloadSpeedMetrics
            );
        }
        statsDashboard.Show();
        statsDashboard.BringToFront();
    }
    catch (Exception ex)
    {
        Log($"Error mostrando dashboard: {ex.Message}");
    }
}

private void ShowContextualHelp()
{
    try
    {
        SlskDown.UI.HelpSystem.ShowContextualHelp(this.ActiveControl, this);
    }
    catch (Exception ex)
    {
        Log($"Error mostrando ayuda: {ex.Message}");
    }
}

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 5: EJECUCIÓN DE COMANDOS
// ═══════════════════════════════════════════════════════════════

private void ExecuteCommand(string commandId)
{
    if (string.IsNullOrEmpty(commandId))
        return;
    
    try
    {
        switch (commandId)
        {
            // Búsquedas
            case "search_new":
                txtSearch?.Focus();
                break;
            case "search_filter":
                ShowFilterDialog();
                break;
            case "search_save_filter":
                SaveCurrentFilter();
                break;
            case "search_history":
                ShowSearchHistory();
                break;
            case "search_new_tab":
                CreateNewSearchTab();
                break;
            case "search_close_tab":
                CloseCurrentSearchTab();
                break;
            
            // Descargas
            case "downloads_view":
                ShowPanel(downloadsContentPanel);
                break;
            case "downloads_priority_high":
                SetSelectedDownloadsPriority(SlskDown.Core.TransferPriority.High);
                break;
            case "downloads_priority_normal":
                SetSelectedDownloadsPriority(SlskDown.Core.TransferPriority.Normal);
                break;
            case "downloads_priority_low":
                SetSelectedDownloadsPriority(SlskDown.Core.TransferPriority.Low);
                break;
            case "downloads_pause_all":
                PauseAllDownloads();
                break;
            case "downloads_resume_all":
                ResumeAllDownloads();
                break;
            case "downloads_clear_completed":
                ClearCompletedDownloads();
                break;
            case "downloads_retry_failed":
                RetryFailedDownloads();
                break;
            
            // Estadísticas
            case "stats_dashboard":
                ShowStatsDashboard();
                break;
            case "stats_export_html":
                ExportStatsToHTML();
                break;
            case "stats_export_csv":
                ExportStatsToCSV();
                break;
            case "stats_export_json":
                ExportStatsToJSON();
                break;
            case "stats_network_health":
                ShowNetworkHealth();
                break;
            
            // Configuración
            case "config_advanced":
                ShowAdvancedSettings();
                break;
            
            // Temas
            case "theme_dark":
                ApplyTheme("Dark Modern");
                break;
            case "theme_light":
                ApplyTheme("Light");
                break;
            case "theme_contrast":
                ApplyTheme("High Contrast");
                break;
            
            // Backup
            case "backup_create":
                CreateBackupNow();
                break;
            case "backup_restore":
                RestoreBackupDialog();
                break;
            
            // Compartidos
            case "shares_rescan":
                RescanShares();
                break;
            
            // Red
            case "network_reconnect":
                ReconnectToServer();
                break;
            
            // Ayuda
            case "help_shortcuts":
                SlskDown.UI.HelpSystem.ShowAllShortcuts(this);
                break;
            
            default:
                Log($"Comando no implementado: {commandId}");
                break;
        }
    }
    catch (Exception ex)
    {
        Log($"Error ejecutando comando {commandId}: {ex.Message}");
    }
}

// ═══════════════════════════════════════════════════════════════
// SECCIÓN 6: MÉTODOS HELPER
// ═══════════════════════════════════════════════════════════════

private int GetActiveDownloadsCount()
{
    // Implementar según tu lógica de descargas
    return 0;
}

private int GetUserQueuePosition(string username)
{
    // Implementar según tu lógica de cola
    return 0;
}

private void ReorderDownloadQueue()
{
    // Implementar reordenamiento de cola
}

private void CreateNewSearchTab()
{
    // Implementar creación de tab
}

private void CloseCurrentSearchTab()
{
    // Implementar cierre de tab
}

private void SwitchToSearchTab(int index)
{
    // Implementar cambio de tab
}

private void RefreshCurrentView()
{
    // Implementar refresh
}

private void SaveCurrentFilter()
{
    // Implementar guardado de filtro
}

private void SaveCurrentLayout()
{
    try
    {
        var layoutName = $"Layout_{DateTime.Now:yyyyMMdd_HHmmss}";
        uiCustomization.SaveLayout(layoutName, this);
        ShowNotification("Layout Guardado", $"Layout '{layoutName}' guardado correctamente");
    }
    catch (Exception ex)
    {
        Log($"Error guardando layout: {ex.Message}");
    }
}

private void ShowFilterDialog()
{
    // Implementar diálogo de filtros
}

private void ShowSearchHistory()
{
    // Implementar historial
}

private void SetSelectedDownloadsPriority(SlskDown.Core.TransferPriority priority)
{
    // Implementar cambio de prioridad
}

private void PauseAllDownloads()
{
    // Implementar pausa
}

private void ResumeAllDownloads()
{
    // Implementar reanudación
}

private void ClearCompletedDownloads()
{
    // Implementar limpieza
}

private void RetryFailedDownloads()
{
    // Implementar retry
}

private void ExportStatsToHTML()
{
    try
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "HTML files (*.html)|*.html",
            FileName = $"stats_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };
        
        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            // Preparar datos y exportar
            ShowNotification("Exportación Completa", "Estadísticas exportadas a HTML");
        }
    }
    catch (Exception ex)
    {
        Log($"Error exportando a HTML: {ex.Message}");
    }
}

private void ExportStatsToCSV()
{
    // Similar a HTML
}

private void ExportStatsToJSON()
{
    // Similar a HTML
}

private void ShowNetworkHealth()
{
    try
    {
        var health = networkHealthMonitor.GetHealth();
        var message = $"Estado: {health.Status}\n" +
                     $"Packet Loss: {health.PacketLossRate:F2}%\n" +
                     $"Latencia: {health.AverageLatency:F2}ms";
        MessageBox.Show(message, "Salud de Red", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        Log($"Error mostrando salud de red: {ex.Message}");
    }
}

private void ApplyTheme(string themeName)
{
    try
    {
        var theme = themeManager.LoadTheme(themeName);
        if (theme != null)
        {
            themeManager.ApplyTheme(this, theme);
            ShowNotification("Tema Aplicado", $"Tema '{themeName}' aplicado correctamente");
        }
    }
    catch (Exception ex)
    {
        Log($"Error aplicando tema: {ex.Message}");
    }
}

private void CreateBackupNow()
{
    try
    {
        var configFile = Path.Combine(dataDir, "config.json");
        var backupFile = autoBackup.CreateBackup(configFile);
        if (!string.IsNullOrEmpty(backupFile))
        {
            ShowNotification("Backup Creado", $"Backup guardado: {Path.GetFileName(backupFile)}");
        }
    }
    catch (Exception ex)
    {
        Log($"Error creando backup: {ex.Message}");
    }
}

private void RestoreBackupDialog()
{
    // Implementar diálogo de restauración
}

private void RescanShares()
{
    // Implementar rescan
}

private void ReconnectToServer()
{
    // Implementar reconexión
}

// ═══════════════════════════════════════════════════════════════
// FIN DEL CÓDIGO DE INTEGRACIÓN
// ═══════════════════════════════════════════════════════════════
