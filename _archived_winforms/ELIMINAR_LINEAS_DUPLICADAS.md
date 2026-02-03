# 🔧 ELIMINAR LÍNEAS DUPLICADAS EN MainForm.cs

**IMPORTANTE:** Quedan 77 errores de variables duplicadas en `MainForm.cs` que deben eliminarse manualmente.

---

## 📍 LÍNEAS A ELIMINAR

Abre `MainForm.cs` en Visual Studio Code y **ELIMINA** las siguientes líneas:

### Bloque 1: Líneas 1121-1189 (aproximadamente)

**Busca en el archivo** (Ctrl+F) las siguientes líneas y **ELIMÍNALAS TODAS**:

```csharp
private ComboBox cmbExtension;
private CheckBox chkSpanishOnly;
private CheckBox chkQualityFilter;
private NumericUpDown numMinQuality;
private CheckBox chkAutoConnect;
private TextBox txtUsername;
private TextBox txtPassword;
private TextBox txtDownloadDir;
private ComboBox cmbFavorites;
private List<string> searchHistory = new List<string>();
private HashSet<string> searchHistorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private List<string> favorites = new List<string>();
private HashSet<string> favoritesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private HashSet<string> blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private HashSet<string> premiumUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private HashSet<string> authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private HashSet<string> normalizedAuthorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private HashSet<string> watchlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.CancellationTokenSource> activeDownloads = new System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.CancellationTokenSource>();
private System.Threading.CancellationTokenSource searchCancellationTokenSource;
private System.Threading.CancellationTokenSource monitoringCancellationTokenSource;
private System.Threading.CancellationTokenSource autoSearchCancellationTokenSource;
private readonly SynchronizationContext synchronizationContext;
private Dictionary<string, string> countryCache = new Dictionary<string, string>();
private List<(dynamic result, ListViewItem item, int retryCount)> retryQueue = new List<(dynamic, ListViewItem, int)>();

private readonly object searchDebounceLock = new object();
private string? lastSearchDebounceKey;
private DateTime lastSearchDebounceStartedUtc = DateTime.MinValue;
private bool isSearchRunning;
private const int SEARCH_DEBOUNCE_MS = 1500;
private int maxRetries = 3;
private NumericUpDown numMaxRetries;
private int searchTimeout = 30;
private bool continuousSearch = true;
private bool instantDownload = false;
private int minFileSizeKB = 0;
private int responseLimit = 5000;
private int fileLimit = 50000;
private int listenPort = 50000;
private TextBox txtFilterResults;
private Button btnClearResults;
private Button btnExportCSV;
private Button btnOpenDownloadFolder;
private Label lblResultsCount;
private Label lblAuthorCount;

// Filtros avanzados
private Panel pnlAdvancedFilters;
private Button btnToggleFilters;
private NumericUpDown numMinSize;
private NumericUpDown numMaxSize;
private ComboBox cmbExtensionFilter;
private ComboBox cmbSortBy;
private CheckBox chkOnlyFreeSlots;
private CheckBox chkExcludeDownloaded;
private Button btnDownloadFiltered;
private bool advancedFiltersVisible = false;
private TextBox txtLog;
private List<string> earlyLogMessages = new List<string>();
private CheckBox chkAutoSpanishDocuments;
private Button btnOpenAutoResults;
```

**IMPORTANTE:** Estas variables YA ESTÁN definidas en otra parte del archivo. Solo elimina las líneas que aparecen DESPUÉS de la línea 1100 aproximadamente.

---

## ✅ VERIFICACIÓN

Después de eliminar las líneas:

1. **Guarda el archivo** (Ctrl+S)
2. **Ejecuta en la terminal:**
   ```cmd
   lanza
   ```
3. **Verifica que los errores disminuyan** de 77 a menos de 10

---

## 🎯 RESULTADO ESPERADO

**Antes:**
- 77 errores de variables duplicadas

**Después:**
- 0-5 errores (solo tipos faltantes como `FixedSizeQueue`, `DownloadStatsSnapshot`, `LibraryItem`, `RecommendationItem`)

---

## 📝 ALTERNATIVA: Usar PowerShell

Si prefieres usar PowerShell para eliminar las líneas automáticamente:

```powershell
cd c:\p2p\SlskDown

# Crear backup
Copy-Item MainForm.cs MainForm.cs.backup_duplicates

# Leer archivo
$lines = Get-Content MainForm.cs

# Eliminar líneas 1121-1189 (ajustar según sea necesario)
$newLines = $lines[0..1120] + $lines[1190..($lines.Length-1)]

# Guardar
$newLines | Set-Content MainForm.cs

# Compilar
lanza
```

---

**Estado:** ✅ Error RustInterop RESUELTO | ⚠️ Quedan 77 errores de duplicados a eliminar manualmente
