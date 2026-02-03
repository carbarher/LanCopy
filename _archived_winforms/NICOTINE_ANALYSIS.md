# Análisis de Nicotine+ para SlskDown

## Resumen
Nicotine+ es el cliente Soulseek de código abierto más avanzado, escrito en Python con GTK. Versión actual: 3.3.10 (Marzo 2025).

**Repositorio**: https://github.com/nicotine-plus/nicotine-plus
**Lenguaje**: Python 3.6+
**UI**: GTK 3/4
**Licencia**: GPL v3.0+

---

## Características Destacadas de Nicotine+ 3.3.0+

### 🔍 **Búsquedas Avanzadas**
1. **Búsqueda por frases exactas** usando comillas: `"this is a phrase"`
2. **Filtros genéricos por tipo de archivo**: audio | image | video | text | archive | executable
3. **Filtro de duración de audio**: HH:MM:SS | MM:SS | Segundos
4. **Filtro de tamaño exacto de archivo**
5. **Restaurar filtros después de limpiarlos**
6. **Filtrar carpetas irrelevantes** al buscar en shares de usuarios
7. **Búsqueda en todas las columnas** de las listas

### 📥 **Gestión de Descargas**
1. **Reintentos inteligentes**: Descargas limitadas por cola/tamaño se reintentan más frecuentemente
2. **Bypass de filtros**: Reanudar una descarga filtrada permite saltarse el filtro
3. **Logs separados por sesión**: download_log.txt y upload_log.txt por sesión
4. **Limpieza automática**: Opción para limpiar descargas finalizadas automáticamente
5. **Esperar uploads activos**: Opción para esperar que terminen uploads antes de cerrar
6. **Timers monotónicos**: Transferencias no afectadas por ajustes del reloj del sistema
7. **Verificación de tiempos modificados**: Chequea archivos en lugar de carpetas al rescanear

### 👥 **Gestión de Usuarios**
1. **Historial de chat**: Popover para ver todos los chats privados anteriores con usuarios
2. **Mensaje masivo**: Enviar mensaje privado a todos los buddies online y usuarios en cola de upload
3. **Usuarios baneados**: No pueden leer descripciones de perfil
4. **Filtro de red**: Verificación de usuarios/IPs baneados antes de compartir

### 📂 **Compartir Archivos**
1. **Shares para buddies**: Opción para hacer shares específicos disponibles solo para buddies de confianza
2. **Path bar**: Barra de ruta al navegar shares de usuarios
3. **Selección múltiple de carpetas**: Permitir seleccionar múltiples carpetas en shares
4. **Advertencia de carpetas no disponibles**: Antes de rescanear
5. **Mejoras de rendimiento**: Al escanear y acceder a shares
6. **Detener carga de shares**: Al cerrar pestaña para ahorrar ancho de banda

### 🎨 **Interfaz de Usuario**
1. **GTK 4**: Nuevo estilo visual moderno
2. **Pestañas importantes**: Marcado de pestañas importantes (highlights)
3. **Reabrir pestaña cerrada**: Ctrl+Shift+T
4. **Menú dropdown**: Lista de todas las pestañas abiertas
5. **Tamaños exactos en bytes**: Opción para mostrar tamaños exactos
6. **Iconos de tipo de archivo**: En listas de archivos
7. **Popovers en barra de estado**: Para límites de velocidad de descarga/upload
8. **Fuente configurable**: Para vistas de texto
9. **Redimensionamiento automático**: De paneles y columnas al cambiar tamaño de ventana
10. **Recordar columna ordenada**: Después de reiniciar
11. **Restaurar orden inicial**: Al presionar header de columna ordenada

### ⚙️ **Configuración y Preferencias**
1. **Idioma de interfaz**: Selección de idioma
2. **Handlers personalizados**: Para abrir archivos descargados
3. **Interfaz de red específica**: Vincular a interfaz de red específica (Windows)
4. **NAT-PMP**: Soporte para port forwarding NAT-PMP
5. **Notificaciones**: Notificación cuando se encuentran resultados de wishlist

### 🔌 **Plugins y Comandos**
1. **Sistema de comandos nuevo**: `/help` para lista de comandos disponibles
2. **Readline en CLI headless**: Edición de comandos y historial con teclado
3. **Leech Detector mejorado**: No envía mensajes a usuarios con conteos incorrectos

### 🌐 **Protocolo y Red**
1. **Protocolo Soulseek completo**: Implementación de distributed peers, versión 160.2
2. **Puerto de escucha**: Solo se abre al conectar al servidor
3. **Soporte Apple Silicon**: Nativo para macOS

---

## Características que PODEMOS Implementar en SlskDown

### ✅ **ALTA PRIORIDAD** (Fácil implementación, alto impacto)

#### 1. **Búsqueda por Frases Exactas**
```csharp
// En SearchAsync(), detectar comillas
if (query.StartsWith("\"") && query.EndsWith("\""))
{
    string exactPhrase = query.Trim('"');
    // Filtrar resultados que contengan la frase exacta
    results = results.Where(r => r.FileName.Contains(exactPhrase, StringComparison.OrdinalIgnoreCase));
}
```

#### 2. **Filtros de Tipo de Archivo Genéricos**
```csharp
// Agregar ComboBox con opciones: Todos, Audio, Video, Imagen, Documento, Archivo, Ejecutable
private static readonly Dictionary<string, string[]> FileTypeFilters = new()
{
    ["Audio"] = new[] { ".mp3", ".flac", ".wav", ".m4a", ".ogg", ".wma" },
    ["Video"] = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" },
    ["Imagen"] = new[] { ".jpg", ".png", ".gif", ".bmp", ".webp", ".svg" },
    ["Documento"] = new[] { ".pdf", ".epub", ".mobi", ".azw3", ".djvu", ".cbr", ".cbz" },
    ["Archivo"] = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" },
    ["Ejecutable"] = new[] { ".exe", ".msi", ".app", ".dmg" }
};
```

#### 3. **Filtro de Duración de Audio**
```csharp
// Agregar NumericUpDown para duración mínima/máxima en minutos
numMinDuration = new NumericUpDown { Value = 0, Maximum = 999 };
numMaxDuration = new NumericUpDown { Value = 0, Maximum = 999 };

// Filtrar en resultados
if (numMinDuration.Value > 0 || numMaxDuration.Value > 0)
{
    results = results.Where(r => {
        int durationMinutes = r.Duration / 60;
        return durationMinutes >= numMinDuration.Value && 
               (numMaxDuration.Value == 0 || durationMinutes <= numMaxDuration.Value);
    });
}
```

#### 4. **Logs Separados por Sesión**
```csharp
// En lugar de un solo log, crear logs por sesión
string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
string downloadLogPath = Path.Combine(dataDir, $"download_log_{sessionId}.txt");
string uploadLogPath = Path.Combine(dataDir, $"upload_log_{sessionId}.txt");
```

#### 5. **Mostrar Tamaños Exactos en Bytes**
```csharp
// Agregar CheckBox "Mostrar tamaños exactos"
chkShowExactSizes = new CheckBox { Text = "Tamaños exactos en bytes" };

// En FormatFileSize()
if (chkShowExactSizes.Checked)
    return $"{bytes:N0} bytes";
else
    return FormatFileSizeHuman(bytes); // KB, MB, GB
```

#### 6. **Reabrir Pestaña Cerrada (Ctrl+Shift+T)**
```csharp
// Stack para pestañas cerradas
private Stack<string> closedTabs = new Stack<string>();

// Al cerrar pestaña
closedTabs.Push(tabName);

// Shortcut Ctrl+Shift+T
this.KeyDown += (s, e) => {
    if (e.Control && e.Shift && e.KeyCode == Keys.T)
    {
        if (closedTabs.Count > 0)
        {
            string tabName = closedTabs.Pop();
            ReopenTab(tabName);
        }
    }
};
```

#### 7. **Recordar Columna Ordenada**
```csharp
// Guardar en config.json
config["lastSortedColumn"] = lvResults.Columns[sortedColumnIndex].Text;
config["lastSortOrder"] = sortOrder.ToString();

// Restaurar al iniciar
if (config.ContainsKey("lastSortedColumn"))
{
    string columnName = config["lastSortedColumn"].ToString();
    var column = lvResults.Columns.Cast<ColumnHeader>()
        .FirstOrDefault(c => c.Text == columnName);
    if (column != null)
        lvResults.ListViewItemSorter = new ListViewColumnSorter(column.Index, sortOrder);
}
```

### ⚠️ **MEDIA PRIORIDAD** (Implementación moderada, buen impacto)

#### 8. **Historial de Chat con Usuarios**
```csharp
// Diccionario para almacenar historial
private Dictionary<string, List<ChatMessage>> chatHistory = new();

// Botón "Ver Historial" en chat privado
btnChatHistory.Click += (s, e) => ShowChatHistory(currentUsername);

private void ShowChatHistory(string username)
{
    var form = new Form { Text = $"Historial con {username}", Size = new Size(600, 400) };
    var listView = new ListView { Dock = DockStyle.Fill, View = View.Details };
    listView.Columns.Add("Fecha", 150);
    listView.Columns.Add("Mensaje", 400);
    
    if (chatHistory.ContainsKey(username))
    {
        foreach (var msg in chatHistory[username])
        {
            listView.Items.Add(new ListViewItem(new[] { 
                msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), 
                msg.Text 
            }));
        }
    }
    
    form.Controls.Add(listView);
    form.ShowDialog();
}
```

#### 9. **Shares Solo para Buddies**
```csharp
// Agregar CheckBox en configuración de shares
chkBuddiesOnly = new CheckBox { Text = "Solo compartir con buddies" };

// En resolución de shares
if (chkBuddiesOnly.Checked && !buddyList.Contains(username))
{
    Log($"⛔ Usuario {username} no es buddy - Share denegado");
    return null;
}
```

#### 10. **Timers Monotónicos para Transferencias**
```csharp
// Usar Stopwatch en lugar de DateTime para medir progreso
private Stopwatch transferStopwatch = new Stopwatch();

// Al iniciar descarga
transferStopwatch.Restart();

// Al calcular velocidad
long elapsedMs = transferStopwatch.ElapsedMilliseconds;
double speedBps = (bytesDownloaded * 1000.0) / elapsedMs;
```

### 🔵 **BAJA PRIORIDAD** (Implementación compleja, impacto variable)

#### 11. **NAT-PMP Port Forwarding**
- Requiere librería externa: `Open.NAT`
- Alternativa a UPnP para algunos routers

#### 12. **Plugins con Sistema de Comandos**
- Requiere arquitectura de plugins completa
- Sistema de comandos `/help`, `/comando`

#### 13. **GTK 4 / UI Moderna**
- SlskDown usa WinForms, migrar a WPF/Avalonia sería un rewrite completo

---

## Características que YA TENEMOS en SlskDown

✅ **Filtro de español** (chkSpanishOnly)
✅ **Filtro de tamaño min/max** (numMinSize, numMaxSize)
✅ **Filtro de extensión** (cmbExtension)
✅ **Lista de favoritos** (cmbFavorites)
✅ **Blacklist de usuarios** (blacklist)
✅ **Auto-conectar** (chkAutoConnect)
✅ **Organizar por autor** (chkOrganizeByAuthor)
✅ **Modo automático** (chkAutoMode)
✅ **Descargas paralelas** (maxParallelDownloads)
✅ **Reintentos automáticos** (maxRetries)
✅ **Búsqueda en múltiples usuarios** (autoSearchAuthors)
✅ **Wishlist** (wishlistItems)
✅ **Historial de descargas** (downloadHistory)

---

## Recomendaciones de Implementación

### **Fase 1: Quick Wins** (1-2 días)
1. Búsqueda por frases exactas con comillas
2. Filtros de tipo de archivo genéricos
3. Mostrar tamaños exactos en bytes
4. Recordar columna ordenada
5. Logs separados por sesión

### **Fase 2: Mejoras de UX** (3-5 días)
6. Filtro de duración de audio
7. Reabrir pestaña cerrada (Ctrl+Shift+T)
8. Historial de chat con usuarios
9. Timers monotónicos para transferencias

### **Fase 3: Características Avanzadas** (1-2 semanas)
10. Shares solo para buddies
11. NAT-PMP port forwarding
12. Sistema de plugins básico

---

## Código de Referencia de Nicotine+

**Búsquedas**: `pynicotine/search.py`
**Transferencias**: `pynicotine/transfers.py`
**Shares**: `pynicotine/shares.py`
**Configuración**: `pynicotine/config.py`
**Protocolo**: `pynicotine/slskmessages.py`

**Documentación del protocolo**: https://nicotine-plus.org/doc/SLSKPROTOCOL.html

---

## Conclusión

Nicotine+ tiene **20+ años de desarrollo** y es el cliente más completo. Las características más valiosas para SlskDown son:

1. ✅ **Búsqueda por frases exactas** - Mejora precisión
2. ✅ **Filtros de tipo de archivo** - Mejor organización
3. ✅ **Filtro de duración** - Para música/audiolibros
4. ✅ **Logs por sesión** - Mejor trazabilidad
5. ✅ **Historial de chat** - Mejor gestión de comunicaciones

Podemos implementar las características de **Fase 1** en 1-2 días con alto impacto en usabilidad.
