# Funcionalidades a Agregar a MainFormClean.cs

## ✅ YA IMPLEMENTADO:
1. Conexión/Desconexión
2. Búsqueda manual
3. Auto-búsqueda de autores con botón unificado
4. 3 pestañas: Resultados, Descargas, Auto-Búsqueda

## 📋 PENDIENTE DE IMPLEMENTAR:

### 1. Variables Adicionales (agregar después de línea 25)
```csharp
// Config
private TextBox usernameTextBox = null!;
private TextBox passwordTextBox = null!;
private TextBox downloadDirTextBox = null!;
private Button saveConfigButton = null!;
private string downloadDir = @"c:\p2p\downloads";

// Filtros
private TextBox filterTextBox = null!;
private ComboBox extensionFilterBox = null!;
private NumericUpDown minSizeBox = null!;
private NumericUpDown maxSizeBox = null!;

// Descargas
private Dictionary<string, DownloadInfo> activeDownloads = new();
private Button downloadSelectedButton = null!;
private Button openFolderButton = null!;
private Label selectedCountLabel = null!;

// Historial y favoritos
private List<string> searchHistory = new();
private List<string> favorites = new();
private Button addToFavoritesButton = null!;
private ComboBox favoritesBox = null!;
```

### 2. Pestaña de Filtros
Agregar después de SetupAutoSearchTab() en SetupUI():
```csharp
// Pestaña Filtros
var filtersTab = new TabPage("🔧 Filtros");
filtersTab.BackColor = Color.FromArgb(45, 45, 48);
tabControl.TabPages.Add(filtersTab);

var filtersPanel = new Panel
{
    Dock = DockStyle.Fill,
    Padding = new Padding(20)
};
filtersTab.Controls.Add(filtersPanel);

// Filtro de texto
var filterLabel = new Label
{
    Text = "Filtrar por texto:",
    Location = new Point(20, 20),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
filtersPanel.Controls.Add(filterLabel);

filterTextBox = new TextBox
{
    Location = new Point(180, 20),
    Size = new Size(300, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White
};
filtersPanel.Controls.Add(filterTextBox);

// Filtro de extensión
var extLabel = new Label
{
    Text = "Extensión:",
    Location = new Point(20, 60),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
filtersPanel.Controls.Add(extLabel);

extensionFilterBox = new ComboBox
{
    Location = new Point(180, 60),
    Size = new Size(150, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    DropDownStyle = ComboBoxStyle.DropDownList
};
extensionFilterBox.Items.AddRange(new object[] { "Todas", "epub", "mobi", "pdf", "azw3", "mp3", "flac" });
extensionFilterBox.SelectedIndex = 0;
filtersPanel.Controls.Add(extensionFilterBox);

// Tamaño mínimo
var minSizeLabel = new Label
{
    Text = "Tamaño mínimo (MB):",
    Location = new Point(20, 100),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
filtersPanel.Controls.Add(minSizeLabel);

minSizeBox = new NumericUpDown
{
    Location = new Point(180, 100),
    Size = new Size(100, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    Minimum = 0,
    Maximum = 10000,
    Value = 0
};
filtersPanel.Controls.Add(minSizeBox);

// Tamaño máximo
var maxSizeLabel = new Label
{
    Text = "Tamaño máximo (MB):",
    Location = new Point(20, 140),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
filtersPanel.Controls.Add(maxSizeLabel);

maxSizeBox = new NumericUpDown
{
    Location = new Point(180, 140),
    Size = new Size(100, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    Minimum = 0,
    Maximum = 10000,
    Value = 0
};
filtersPanel.Controls.Add(maxSizeBox);

// Botón aplicar filtros
var applyFiltersButton = new Button
{
    Text = "✓ Aplicar Filtros",
    Location = new Point(20, 190),
    Size = new Size(150, 35),
    BackColor = Color.FromArgb(40, 167, 69),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat
};
applyFiltersButton.Click += ApplyFiltersButton_Click;
filtersPanel.Controls.Add(applyFiltersButton);
```

### 3. Pestaña de Configuración
Agregar después de la pestaña de Filtros:
```csharp
// Pestaña Config
var configTab = new TabPage("⚙️ Config");
configTab.BackColor = Color.FromArgb(45, 45, 48);
tabControl.TabPages.Add(configTab);

var configPanel = new Panel
{
    Dock = DockStyle.Fill,
    Padding = new Padding(20)
};
configTab.Controls.Add(configPanel);

// Usuario
var userLabel = new Label
{
    Text = "Usuario:",
    Location = new Point(20, 20),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
configPanel.Controls.Add(userLabel);

usernameTextBox = new TextBox
{
    Location = new Point(180, 20),
    Size = new Size(300, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    Text = username
};
configPanel.Controls.Add(usernameTextBox);

// Contraseña
var passLabel = new Label
{
    Text = "Contraseña:",
    Location = new Point(20, 60),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
configPanel.Controls.Add(passLabel);

passwordTextBox = new TextBox
{
    Location = new Point(180, 60),
    Size = new Size(300, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    Text = password,
    UseSystemPasswordChar = true
};
configPanel.Controls.Add(passwordTextBox);

// Carpeta de descargas
var dirLabel = new Label
{
    Text = "Carpeta descargas:",
    Location = new Point(20, 100),
    Size = new Size(150, 20),
    ForeColor = Color.White
};
configPanel.Controls.Add(dirLabel);

downloadDirTextBox = new TextBox
{
    Location = new Point(180, 100),
    Size = new Size(300, 25),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    Text = downloadDir
};
configPanel.Controls.Add(downloadDirTextBox);

// Botón guardar
saveConfigButton = new Button
{
    Text = "💾 Guardar Configuración",
    Location = new Point(20, 150),
    Size = new Size(200, 35),
    BackColor = Color.FromArgb(40, 167, 69),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat
};
saveConfigButton.Click += SaveConfigButton_Click;
configPanel.Controls.Add(saveConfigButton);
```

### 4. Botones Adicionales en Panel Superior
Agregar en SetupUI() después del searchButton:
```csharp
addToFavoritesButton = new Button
{
    Text = "⭐ Favorito",
    Location = new Point(740, 15),
    Size = new Size(100, 30),
    BackColor = Color.FromArgb(255, 193, 7),
    ForeColor = Color.Black,
    FlatStyle = FlatStyle.Flat
};
addToFavoritesButton.Click += AddToFavoritesButton_Click;
topPanel.Controls.Add(addToFavoritesButton);

favoritesBox = new ComboBox
{
    Location = new Point(850, 15),
    Size = new Size(200, 30),
    BackColor = Color.FromArgb(60, 60, 65),
    ForeColor = Color.White,
    DropDownStyle = ComboBoxStyle.DropDownList
};
favoritesBox.SelectedIndexChanged += FavoritesBox_SelectedIndexChanged;
topPanel.Controls.Add(favoritesBox);
```

### 5. Botones en Pestaña Resultados
Agregar antes de resultsListView:
```csharp
var buttonPanel = new Panel
{
    Dock = DockStyle.Top,
    Height = 40,
    BackColor = Color.FromArgb(35, 35, 38)
};
resultsTab.Controls.Add(buttonPanel);

downloadSelectedButton = new Button
{
    Text = "📥 Descargar Seleccionados",
    Location = new Point(10, 5),
    Size = new Size(180, 30),
    BackColor = Color.FromArgb(40, 167, 69),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat
};
downloadSelectedButton.Click += DownloadSelectedButton_Click;
buttonPanel.Controls.Add(downloadSelectedButton);

var selectAllButton = new Button
{
    Text = "✓ Seleccionar Todo",
    Location = new Point(200, 5),
    Size = new Size(130, 30),
    BackColor = Color.FromArgb(0, 120, 215),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat
};
selectAllButton.Click += (s, e) => { foreach (ListViewItem item in resultsListView.Items) item.Selected = true; };
buttonPanel.Controls.Add(selectAllButton);
```

### 6. Modificar resultsListView
Agregar después de crear resultsListView:
```csharp
resultsListView.MultiSelect = true;
resultsListView.SelectedIndexChanged += (s, e) => 
    selectedCountLabel.Text = $"Seleccionados: {resultsListView.SelectedItems.Count}";
resultsListView.DoubleClick += ResultsListView_DoubleClick;
```

### 7. Modificar SearchButton_Click
Agregar Tag a los items:
```csharp
item.Tag = new SearchResultData
{
    Username = result.Username,
    Filename = file.Filename,
    Size = file.Size
};
```

### 8. Nuevos Event Handlers (agregar al final antes de FormatFileSize)
```csharp
private void AddToFavoritesButton_Click(object? sender, EventArgs e)
{
    string query = searchBox.Text.Trim();
    if (string.IsNullOrEmpty(query)) return;

    if (!favorites.Contains(query))
    {
        favorites.Add(query);
        favoritesBox.Items.Add(query);
        SaveFavorites();
        MessageBox.Show("Agregado a favoritos", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

private void FavoritesBox_SelectedIndexChanged(object? sender, EventArgs e)
{
    if (favoritesBox.SelectedItem != null)
    {
        searchBox.Text = favoritesBox.SelectedItem.ToString();
    }
}

private async void DownloadSelectedButton_Click(object? sender, EventArgs e)
{
    if (resultsListView.SelectedItems.Count == 0)
    {
        MessageBox.Show("Selecciona archivos para descargar", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    foreach (ListViewItem item in resultsListView.SelectedItems)
    {
        if (item.Tag is SearchResultData data)
        {
            await DownloadFile(data);
        }
    }
}

private async void ResultsListView_DoubleClick(object? sender, EventArgs e)
{
    if (resultsListView.SelectedItems.Count > 0)
    {
        var item = resultsListView.SelectedItems[0];
        if (item.Tag is SearchResultData data)
        {
            await DownloadFile(data);
        }
    }
}

private async Task DownloadFile(SearchResultData data)
{
    if (client?.State != SoulseekClientStates.Connected) return;

    string key = $"{data.Username}_{Path.GetFileName(data.Filename)}";
    if (activeDownloads.ContainsKey(key)) return;

    try
    {
        Directory.CreateDirectory(downloadDir);
        string localPath = Path.Combine(downloadDir, Path.GetFileName(data.Filename));

        var downloadInfo = new DownloadInfo
        {
            Username = data.Username,
            Filename = data.Filename,
            LocalPath = localPath,
            TotalBytes = data.Size
        };
        activeDownloads[key] = downloadInfo;

        var downloadItem = new ListViewItem(new string[]
        {
            Path.GetFileName(data.Filename),
            "0%",
            "0 KB/s",
            "Descargando..."
        });
        downloadItem.Tag = key;
        downloadsListView.Items.Add(downloadItem);

        await client.DownloadAsync(
            data.Username,
            data.Filename,
            localPath,
            options: new TransferOptions(
                progressUpdated: (progress) =>
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(() => UpdateDownloadProgress(key, progress));
                    }
                    else
                    {
                        UpdateDownloadProgress(key, progress);
                    }
                }
            )
        );

        UpdateDownloadStatus(key, "Completado");
    }
    catch (Exception ex)
    {
        UpdateDownloadStatus(key, $"Error: {ex.Message}");
    }
}

private void UpdateDownloadProgress(string key, TransferProgressUpdatedEventArgs progress)
{
    if (!activeDownloads.ContainsKey(key)) return;

    var info = activeDownloads[key];
    info.BytesDownloaded = progress.BytesTransferred;

    foreach (ListViewItem item in downloadsListView.Items)
    {
        if (item.Tag?.ToString() == key)
        {
            double percent = (double)progress.BytesTransferred / progress.Size * 100;
            item.SubItems[1].Text = $"{percent:0.0}%";
            item.SubItems[2].Text = $"{FormatFileSize((long)progress.AverageSpeed)}/s";
            break;
        }
    }
}

private void UpdateDownloadStatus(string key, string status)
{
    foreach (ListViewItem item in downloadsListView.Items)
    {
        if (item.Tag?.ToString() == key)
        {
            item.SubItems[3].Text = status;
            break;
        }
    }
}

private void ApplyFiltersButton_Click(object? sender, EventArgs e)
{
    string textFilter = filterTextBox.Text.Trim().ToLower();
    string extFilter = extensionFilterBox.SelectedItem?.ToString() ?? "Todas";
    long minSize = (long)(minSizeBox.Value * 1024 * 1024);
    long maxSize = (long)(maxSizeBox.Value * 1024 * 1024);

    foreach (ListViewItem item in resultsListView.Items)
    {
        bool visible = true;

        // Filtro de texto
        if (!string.IsNullOrEmpty(textFilter))
        {
            string filename = item.SubItems[1].Text.ToLower();
            if (!filename.Contains(textFilter))
                visible = false;
        }

        // Filtro de extensión
        if (extFilter != "Todas")
        {
            string ext = item.SubItems[3].Text;
            if (!ext.Equals(extFilter, StringComparison.OrdinalIgnoreCase))
                visible = false;
        }

        // Filtro de tamaño
        if (item.Tag is SearchResultData data)
        {
            if (minSize > 0 && data.Size < minSize)
                visible = false;
            if (maxSize > 0 && data.Size > maxSize)
                visible = false;
        }

        item.Remove();
        if (visible)
            resultsListView.Items.Add(item);
    }

    statusLabel.Text = $"Filtros aplicados - {resultsListView.Items.Count} resultados";
}

private void SaveConfigButton_Click(object? sender, EventArgs e)
{
    username = usernameTextBox.Text.Trim();
    password = passwordTextBox.Text;
    downloadDir = downloadDirTextBox.Text.Trim();
    
    SaveConfig();
    MessageBox.Show("Configuración guardada", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
}

private void LoadConfig()
{
    try
    {
        string configFile = "config.json";
        if (System.IO.File.Exists(configFile))
        {
            var json = System.IO.File.ReadAllText(configFile);
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            if (config != null)
            {
                username = config.Username;
                password = config.Password;
                downloadDir = config.DownloadDir;
            }
        }
    }
    catch { }
}

private void SaveConfig()
{
    try
    {
        var config = new AppConfig
        {
            Username = username,
            Password = password,
            DownloadDir = downloadDir
        };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText("config.json", json);
    }
    catch { }
}

private void LoadSearchHistory()
{
    try
    {
        string historyFile = "search_history.json";
        if (System.IO.File.Exists(historyFile))
        {
            var json = System.IO.File.ReadAllText(historyFile);
            searchHistory = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            foreach (var item in searchHistory)
                searchBox.Items.Add(item);
        }
    }
    catch { }
}

private void SaveSearchHistory()
{
    try
    {
        var json = JsonSerializer.Serialize(searchHistory);
        System.IO.File.WriteAllText("search_history.json", json);
    }
    catch { }
}

private void LoadFavorites()
{
    try
    {
        string favoritesFile = "favorites.json";
        if (System.IO.File.Exists(favoritesFile))
        {
            var json = System.IO.File.ReadAllText(favoritesFile);
            favorites = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            foreach (var item in favorites)
                favoritesBox.Items.Add(item);
        }
    }
    catch { }
}

private void SaveFavorites()
{
    try
    {
        var json = JsonSerializer.Serialize(favorites);
        System.IO.File.WriteAllText("favorites.json", json);
    }
    catch { }
}
```

## 🚀 COMPILAR
```batch
c:\p2p\SlskDown\compile_clean.bat
```

## ✅ FUNCIONALIDADES COMPLETAS:
1. Auto-conexión al iniciar
2. 7 columnas con datos completos
3. Filtros avanzados (tamaño, extensión, texto)
4. Favoritos con ComboBox
5. Filtro en tiempo real
6. Selección múltiple
7. Ordenamiento de columnas
8. Menú contextual
9. Botones de acción
10. Atajos de teclado
11. Historial de búsquedas
12. Contador de selección
13. Descargas funcionales
14. Botón Abrir carpeta
15. Pestaña Config
16. Auto-búsqueda con botón unificado
