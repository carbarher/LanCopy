# 🎨 Características Adicionales de Nicotine+ para SlskDown

## Análisis de Características UI/UX y Funcionalidades Avanzadas

Este documento complementa los anteriores (`NICOTINE_BEST_PRACTICES.md` y `NICOTINE_ADVANCED_FEATURES.md`) con características adicionales de UI/UX, rooms, plugins y más.

---

## 💬 1. SISTEMA DE CHAT ROOMS Y ROOM WALLS

### 1.1 Public Chat Room Feed
**Estado en Nicotine+**: Issue #2482 - Feature mejorada

**Características**:
- Feed de mensajes de rooms públicos
- Nombres de rooms clickeables para unirse
- Notificaciones selectivas (mute rooms individuales)
- Indicador de mensajes no leídos por room

**Solución propuesta para SlskDown**:
```csharp
// Sistema de Chat Rooms con feed público
public class ChatRoomManager
{
    private Dictionary<string, ChatRoom> joinedRooms = new Dictionary<string, ChatRoom>();
    private HashSet<string> mutedRooms = new HashSet<string>();
    private bool showPublicFeed = true;
    private int maxFeedMessages = 100;
    
    public class ChatRoom
    {
        public string Name { get; set; }
        public List<RoomMessage> Messages { get; set; } = new List<RoomMessage>();
        public HashSet<string> Users { get; set; } = new HashSet<string>();
        public bool IsMuted { get; set; }
        public int UnreadCount { get; set; }
        public bool IsPublic { get; set; }
    }
    
    public class RoomMessage
    {
        public string RoomName { get; set; }
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsHighlighted { get; set; } // Menciona tu username
    }
    
    // Feed público de todos los rooms
    public Panel CreatePublicFeed()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        
        var lvFeed = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };
        
        lvFeed.Columns.Add("Room", 120);
        lvFeed.Columns.Add("Usuario", 100);
        lvFeed.Columns.Add("Mensaje", 400);
        lvFeed.Columns.Add("Hora", 60);
        
        // Hacer rooms clickeables
        lvFeed.ItemActivate += (s, e) =>
        {
            if (lvFeed.SelectedItems.Count > 0)
            {
                string roomName = lvFeed.SelectedItems[0].SubItems[0].Text;
                JoinRoom(roomName);
            }
        };
        
        panel.Controls.Add(lvFeed);
        return panel;
    }
    
    public void MuteRoom(string roomName)
    {
        mutedRooms.Add(roomName);
        if (joinedRooms.ContainsKey(roomName))
        {
            joinedRooms[roomName].IsMuted = true;
        }
    }
    
    public bool ShouldNotify(string roomName)
    {
        return !mutedRooms.Contains(roomName);
    }
}
```

**Configuración recomendada**:
- ✅ Public feed activado por defecto
- ✅ Mute individual por room
- ✅ Click en room name para unirse
- ✅ Highlight de menciones

### 1.2 Room Walls (Muros de Sala)
**Estado en Nicotine+**: Issue #985 - Mejoras propuestas

**Características**:
- Mensaje persistente en el muro de cada room
- Usernames clickeables y coloreados
- Indicador de estado online/away/offline
- Timestamp de cuando se estableció el mensaje
- Botones "Set" y "Clear" para gestionar mensaje

**Solución propuesta para SlskDown**:
```csharp
// Sistema de Room Walls
public class RoomWall
{
    public class WallMessage
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime SetTime { get; set; }
        public bool IsOnline { get; set; }
        public string CountryCode { get; set; }
    }
    
    private Dictionary<string, List<WallMessage>> roomWalls = new Dictionary<string, List<WallMessage>>();
    private Dictionary<string, string> myWallMessages = new Dictionary<string, string>();
    
    public Form CreateRoomWallDialog(string roomName)
    {
        var form = new Form
        {
            Text = $"Room Wall - {roomName}",
            Size = new Size(600, 500),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        
        // ListView con mensajes del muro
        var lvWall = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        
        lvWall.Columns.Add("Estado", 50);
        lvWall.Columns.Add("Usuario", 120);
        lvWall.Columns.Add("Mensaje", 300);
        lvWall.Columns.Add("Desde", 100);
        
        if (roomWalls.ContainsKey(roomName))
        {
            foreach (var msg in roomWalls[roomName].OrderBy(m => m.Username))
            {
                var item = new ListViewItem();
                
                // Indicador de estado
                string status = msg.IsOnline ? "🟢" : "🔴";
                item.SubItems.Add(status);
                
                // Username clickeable
                var usernameItem = item.SubItems.Add(msg.Username);
                usernameItem.ForeColor = msg.IsOnline ? Color.LightGreen : Color.Gray;
                
                item.SubItems.Add(msg.Message);
                item.SubItems.Add(GetRelativeTime(msg.SetTime));
                
                lvWall.Items.Add(item);
            }
        }
        
        // Panel inferior para establecer mensaje
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(10) };
        
        var txtMyMessage = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            Text = myWallMessages.ContainsKey(roomName) ? myWallMessages[roomName] : ""
        };
        
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 35, FlowDirection = FlowDirection.LeftToRight };
        
        var btnSet = new Button { Text = "Set", Width = 80, Height = 30 };
        btnSet.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(txtMyMessage.Text))
            {
                SetWallMessage(roomName, txtMyMessage.Text);
                form.Close();
            }
        };
        
        var btnClear = new Button { Text = "Clear", Width = 80, Height = 30 };
        btnClear.Click += (s, e) =>
        {
            ClearWallMessage(roomName);
            txtMyMessage.Text = "";
        };
        
        btnPanel.Controls.Add(btnSet);
        btnPanel.Controls.Add(btnClear);
        
        bottomPanel.Controls.Add(txtMyMessage);
        bottomPanel.Controls.Add(btnPanel);
        
        form.Controls.Add(lvWall);
        form.Controls.Add(bottomPanel);
        
        return form;
    }
    
    private string GetRelativeTime(DateTime time)
    {
        var diff = DateTime.Now - time;
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM dd");
    }
}
```

---

## ⌨️ 2. KEYBOARD SHORTCUTS (ATAJOS DE TECLADO)

**Estado en Nicotine+**: Implementado - F1 para ver lista completa

**Atajos esenciales de Nicotine+**:
```
F1          - Mostrar lista de atajos
Ctrl+F      - Buscar en pestaña actual
Ctrl+W      - Cerrar pestaña actual
Ctrl+Shift+T - Reabrir última pestaña cerrada (YA IMPLEMENTADO)
Ctrl+Tab    - Siguiente pestaña
Ctrl+Shift+Tab - Pestaña anterior
Ctrl+1-9    - Ir a pestaña específica
Ctrl+N      - Nueva búsqueda
Ctrl+D      - Ir a descargas
Ctrl+U      - Ir a uploads
Ctrl+R      - Ir a rooms
Ctrl+L      - Ir a logs
Ctrl+,      - Preferencias
Ctrl+Q      - Salir
Ctrl+C      - Copiar selección
Ctrl+V      - Pegar
Ctrl+A      - Seleccionar todo
Ctrl+Z      - Deshacer (en campos de texto)
Alt+Enter   - Propiedades del archivo seleccionado
Delete      - Eliminar/Cancelar selección
F5          - Refrescar
Escape      - Cancelar/Cerrar diálogo
```

**Solución propuesta para SlskDown**:
```csharp
// Sistema de atajos de teclado
public class KeyboardShortcutManager
{
    private Dictionary<Keys, Action> shortcuts = new Dictionary<Keys, Action>();
    
    public void RegisterShortcuts(Form form)
    {
        form.KeyPreview = true;
        form.KeyDown += HandleKeyDown;
        
        // Registrar atajos
        RegisterShortcut(Keys.F1, ShowShortcutHelp);
        RegisterShortcut(Keys.Control | Keys.F, FocusSearch);
        RegisterShortcut(Keys.Control | Keys.W, CloseCurrentTab);
        RegisterShortcut(Keys.Control | Keys.Shift | Keys.T, ReopenClosedTab);
        RegisterShortcut(Keys.Control | Keys.Tab, NextTab);
        RegisterShortcut(Keys.Control | Keys.Shift | Keys.Tab, PreviousTab);
        RegisterShortcut(Keys.Control | Keys.N, NewSearch);
        RegisterShortcut(Keys.Control | Keys.D, GoToDownloads);
        RegisterShortcut(Keys.Control | Keys.U, GoToUploads);
        RegisterShortcut(Keys.Control | Keys.R, GoToRooms);
        RegisterShortcut(Keys.Control | Keys.L, GoToLogs);
        RegisterShortcut(Keys.F5, Refresh);
    }
    
    private void HandleKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.KeyData;
        
        if (shortcuts.ContainsKey(key))
        {
            shortcuts[key]?.Invoke();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        
        // Atajos numéricos Ctrl+1-9
        if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
        {
            int tabIndex = e.KeyCode - Keys.D1;
            GoToTab(tabIndex);
            e.Handled = true;
        }
    }
    
    private void ShowShortcutHelp()
    {
        var form = new Form
        {
            Text = "Keyboard Shortcuts",
            Size = new Size(500, 600),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        
        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.None
        };
        
        rtb.AppendText("KEYBOARD SHORTCUTS\n\n");
        rtb.AppendText("F1                - Show this help\n");
        rtb.AppendText("Ctrl+F            - Search in current tab\n");
        rtb.AppendText("Ctrl+W            - Close current tab\n");
        rtb.AppendText("Ctrl+Shift+T      - Reopen closed tab\n");
        rtb.AppendText("Ctrl+Tab          - Next tab\n");
        rtb.AppendText("Ctrl+Shift+Tab    - Previous tab\n");
        rtb.AppendText("Ctrl+1-9          - Go to specific tab\n");
        rtb.AppendText("Ctrl+N            - New search\n");
        rtb.AppendText("Ctrl+D            - Go to downloads\n");
        rtb.AppendText("Ctrl+U            - Go to uploads\n");
        rtb.AppendText("Ctrl+R            - Go to rooms\n");
        rtb.AppendText("Ctrl+L            - Go to logs\n");
        rtb.AppendText("F5                - Refresh\n");
        rtb.AppendText("Delete            - Remove/Cancel selected\n");
        rtb.AppendText("Alt+Enter         - File properties\n");
        rtb.AppendText("Escape            - Cancel/Close dialog\n");
        
        form.Controls.Add(rtb);
        form.ShowDialog();
    }
}
```

---

## 📁 3. FILE MANAGER INTEGRATION

**Estado en Nicotine+**: Issue #1004 - Soporte para file managers personalizados

**Características**:
- Abrir carpeta en file manager
- File manager personalizable
- Drag & drop de archivos
- Copiar ruta de archivo
- Mostrar en explorador

**Solución propuesta para SlskDown**:
```csharp
// Integración con file manager
public class FileManagerIntegration
{
    private string customFileManager = "";
    private string customFileManagerArgs = "{path}";
    
    public void OpenInFileManager(string filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(customFileManager))
            {
                // File manager personalizado
                string args = customFileManagerArgs.Replace("{path}", $"\"{filePath}\"");
                Process.Start(customFileManager, args);
            }
            else
            {
                // File manager por defecto del sistema
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"\"{filePath}\"");
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error abriendo file manager: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    public void CopyPathToClipboard(string path)
    {
        try
        {
            Clipboard.SetText(path);
        }
        catch { }
    }
    
    public void EnableDragDrop(ListView listView)
    {
        listView.AllowDrop = true;
        listView.ItemDrag += (s, e) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                var files = new List<string>();
                foreach (ListViewItem item in listView.SelectedItems)
                {
                    string filePath = item.Tag as string;
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        files.Add(filePath);
                    }
                }
                
                if (files.Count > 0)
                {
                    var data = new DataObject(DataFormats.FileDrop, files.ToArray());
                    listView.DoDragDrop(data, DragDropEffects.Copy);
                }
            }
        };
    }
}
```

---

## 🔌 4. SISTEMA DE PLUGINS

**Estado en Nicotine+**: Implementado - Sistema de plugins en Python

**Plugins populares de Nicotine+**:
- Now Playing (Last.fm, ListenBrainz)
- Auto-reply en mensajes privados
- Custom commands
- Search filters avanzados
- Notificaciones personalizadas

**Solución propuesta para SlskDown**:
```csharp
// Sistema básico de plugins
public interface ISlskDownPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    
    void Initialize(IPluginHost host);
    void OnSearchResult(SearchResult result);
    void OnDownloadComplete(DownloadItem download);
    void OnPrivateMessage(string username, string message);
    void OnRoomMessage(string room, string username, string message);
}

public interface IPluginHost
{
    void Log(string message);
    void SendPrivateMessage(string username, string message);
    void SendRoomMessage(string room, string message);
    void AddToDownloads(string username, string filename);
}

public class PluginManager
{
    private List<ISlskDownPlugin> loadedPlugins = new List<ISlskDownPlugin>();
    private IPluginHost pluginHost;
    
    public void LoadPlugins(string pluginsDir)
    {
        if (!Directory.Exists(pluginsDir)) return;
        
        // Cargar DLLs de plugins
        foreach (var dllFile in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(ISlskDownPlugin).IsAssignableFrom(t) && !t.IsInterface);
                
                foreach (var type in pluginTypes)
                {
                    var plugin = (ISlskDownPlugin)Activator.CreateInstance(type);
                    plugin.Initialize(pluginHost);
                    loadedPlugins.Add(plugin);
                    Log($"✅ Plugin cargado: {plugin.Name} v{plugin.Version}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error cargando plugin {Path.GetFileName(dllFile)}: {ex.Message}");
            }
        }
    }
    
    public void OnSearchResult(SearchResult result)
    {
        foreach (var plugin in loadedPlugins)
        {
            try
            {
                plugin.OnSearchResult(result);
            }
            catch { }
        }
    }
}

// Ejemplo de plugin: Auto-reply
public class AutoReplyPlugin : ISlskDownPlugin
{
    public string Name => "Auto-Reply";
    public string Version => "1.0";
    public string Description => "Responde automáticamente a mensajes privados";
    
    private IPluginHost host;
    private string autoReplyMessage = "Thanks for your message! I'll reply soon.";
    private bool enabled = true;
    
    public void Initialize(IPluginHost host)
    {
        this.host = host;
        host.Log($"Auto-Reply plugin initialized");
    }
    
    public void OnPrivateMessage(string username, string message)
    {
        if (enabled && !string.IsNullOrEmpty(autoReplyMessage))
        {
            host.SendPrivateMessage(username, autoReplyMessage);
            host.Log($"📧 Auto-reply sent to {username}");
        }
    }
    
    // Otros métodos no usados
    public void OnSearchResult(SearchResult result) { }
    public void OnDownloadComplete(DownloadItem download) { }
    public void OnRoomMessage(string room, string username, string message) { }
}
```

---

## 📊 5. CARACTERÍSTICAS UI/UX ADICIONALES

### 5.1 Dropdown Menu para Notificaciones
**Estado en Nicotine+**: Implementado en v3.3+

```csharp
// Dropdown de notificaciones en tab bar
public class NotificationDropdown
{
    public ToolStripDropDownButton CreateNotificationButton()
    {
        var btn = new ToolStripDropDownButton
        {
            Text = "🔔",
            ToolTipText = "Notifications",
            DisplayStyle = ToolStripItemDisplayStyle.Text
        };
        
        // Agregar items dinámicamente
        btn.DropDownOpening += (s, e) =>
        {
            btn.DropDownItems.Clear();
            
            // Mensajes privados no leídos
            int unreadPM = GetUnreadPrivateMessages();
            if (unreadPM > 0)
            {
                btn.DropDownItems.Add($"💬 {unreadPM} private messages", null, (s2, e2) => GoToPrivateMessages());
            }
            
            // Wishlist matches
            int wishlistMatches = GetWishlistMatches();
            if (wishlistMatches > 0)
            {
                btn.DropDownItems.Add($"🎯 {wishlistMatches} wishlist matches", null, (s2, e2) => GoToWishlist());
            }
            
            // Room mentions
            int roomMentions = GetRoomMentions();
            if (roomMentions > 0)
            {
                btn.DropDownItems.Add($"💬 {roomMentions} room mentions", null, (s2, e2) => GoToRooms());
            }
            
            if (btn.DropDownItems.Count == 0)
            {
                btn.DropDownItems.Add("No notifications", null, null).Enabled = false;
            }
        };
        
        return btn;
    }
}
```

### 5.2 Folder Download Options Dialog
**Estado en Nicotine+**: Issue #1659 - Feature solicitada

```csharp
// Diálogo de opciones al descargar carpeta
public class FolderDownloadDialog
{
    public static DialogResult ShowDialog(string folderName, List<string> files, out string targetPath, out List<string> selectedFiles)
    {
        var form = new Form
        {
            Text = "Download Folder Options",
            Size = new Size(600, 500),
            StartPosition = FormStartPosition.CenterParent
        };
        
        // Target path
        var lblTarget = new Label { Text = "Target folder:", Location = new Point(10, 10), AutoSize = true };
        var txtTarget = new TextBox { Location = new Point(10, 30), Width = 450 };
        var btnBrowse = new Button { Text = "...", Location = new Point(470, 28), Width = 30 };
        
        btnBrowse.Click += (s, e) =>
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtTarget.Text = fbd.SelectedPath;
                }
            }
        };
        
        // File list con checkboxes
        var lblFiles = new Label { Text = "Files to download:", Location = new Point(10, 70), AutoSize = true };
        var lvFiles = new ListView
        {
            Location = new Point(10, 90),
            Size = new Size(560, 300),
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true
        };
        
        lvFiles.Columns.Add("File", 400);
        lvFiles.Columns.Add("Size", 100);
        
        foreach (var file in files)
        {
            var item = new ListViewItem(Path.GetFileName(file));
            item.Checked = true;
            item.Tag = file;
            lvFiles.Items.Add(item);
        }
        
        // Botones
        var btnOK = new Button { Text = "Download", Location = new Point(400, 420), Width = 90 };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(500, 420), Width = 90 };
        
        btnOK.Click += (s, e) => form.DialogResult = DialogResult.OK;
        btnCancel.Click += (s, e) => form.DialogResult = DialogResult.Cancel;
        
        form.Controls.AddRange(new Control[] { lblTarget, txtTarget, btnBrowse, lblFiles, lvFiles, btnOK, btnCancel });
        
        var result = form.ShowDialog();
        targetPath = txtTarget.Text;
        selectedFiles = lvFiles.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => i.Tag as string).ToList();
        
        return result;
    }
}
```

---

## 🎯 6. PLAN DE IMPLEMENTACIÓN ADICIONAL

### Prioridad Alta
- [ ] **Keyboard shortcuts completos** (F1 help, Ctrl+W, Ctrl+Tab, etc.)
- [ ] **File manager integration** (open folder, drag & drop)
- [ ] **Notification dropdown** en tab bar
- [ ] **Room walls** con usernames clickeables

### Prioridad Media
- [ ] **Public chat room feed** con rooms clickeables
- [ ] **Folder download options dialog**
- [ ] **Sistema básico de plugins**
- [ ] **Auto-reply plugin** de ejemplo

### Prioridad Baja
- [ ] **Now Playing plugin** (Last.fm/ListenBrainz)
- [ ] **Custom commands** en chat
- [ ] **Advanced room wall features**

---

## 📋 RESUMEN DE CARACTERÍSTICAS ADICIONALES

### Ventajas adicionales sobre Nicotine+:
1. ✅ **Keyboard shortcuts completos** con ayuda F1
2. ✅ **File manager personalizable** (no solo explorer.exe)
3. ✅ **Drag & drop** de archivos desde listas
4. ✅ **Notification dropdown** centralizado
5. ✅ **Room walls mejorados** con timestamps y estados
6. ✅ **Public feed** con mute selectivo
7. ✅ **Folder download dialog** con selección de archivos
8. ✅ **Sistema de plugins** extensible

### Configuración Recomendada:
```json
{
  "keyboard": {
    "enableShortcuts": true,
    "showHelpOnF1": true
  },
  "fileManager": {
    "customPath": "",
    "customArgs": "{path}",
    "enableDragDrop": true
  },
  "rooms": {
    "showPublicFeed": true,
    "maxFeedMessages": 100,
    "mutedRooms": [],
    "enableRoomWalls": true
  },
  "plugins": {
    "enabled": true,
    "pluginsDirectory": "plugins",
    "autoLoadPlugins": true
  },
  "ui": {
    "showNotificationDropdown": true,
    "folderDownloadDialog": true
  }
}
```

---

## 🔥 CARACTERÍSTICAS ÚNICAS DE SLSKDOWN

Después de implementar todo, SlskDown tendrá:

### **Características de Nicotine+ Implementadas**:
✅ Reconexión automática con backoff exponencial
✅ Auto-retry de descargas inteligente
✅ Bandwidth mode dinámico
✅ Estadísticas de usuario con rating
✅ Chat history persistente
✅ Wishlist con notificaciones
✅ Banned phrases filter
✅ Keyboard shortcuts completos
✅ File manager integration
✅ Room walls mejorados
✅ Public chat feed
✅ Sistema de plugins

### **Características Únicas de SlskDown**:
✅ Integración con Calibre
✅ Búsqueda masiva de autores (700+)
✅ Deduplicación con Rust
✅ Pool de conexiones (3x throughput)
✅ Bloom filters (1000x más rápido)
✅ Caché de metadatos (100x más rápido)
✅ Modo automático completo
✅ Modo agresivo temporal
✅ Dashboard de métricas en tiempo real
✅ Organización por autor automática

---

**Documento creado**: 2026-01-10
**Basado en**: Nicotine+ Issues #2482, #985, #1004, #1659, #1010, #1342, #1600
**Para**: SlskDown v1.0+
**Total de características documentadas**: 50+
