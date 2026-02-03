# 🌙 Sistema de Modo Oscuro

## Descripción General

SlskDown implementa un sistema completo de modo oscuro nativo para Windows, proporcionando una experiencia visual consistente y moderna en toda la aplicación.

## Componentes del Sistema

### 1. **DarkMessageBox** (`UI/DarkMessageBox.cs`)
MessageBox personalizado con tema oscuro completo.

**Características:**
- Fondo oscuro (#1E1E1E)
- Texto blanco con alta legibilidad
- Botones con estilo moderno y hover effects
- Iconos de sistema (Information, Warning, Error, Question)
- Soporte para múltiples botones (OK, OKCancel, YesNo, YesNoCancel)
- Redimensionamiento automático según contenido
- Centrado en pantalla o respecto al formulario padre

**Uso:**
```csharp
DarkMessageBox.Show("Mensaje", "Título", MessageBoxButtons.OK, MessageBoxIcon.Information);
DarkMessageBox.Show(this, "¿Continuar?", "Confirmación", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
```

### 2. **DarkFolderBrowserDialog** (`UI/DarkFolderBrowserDialog.cs`)
Diálogo de selección de carpetas con modo oscuro.

**Características:**
- Integración con Windows API para modo oscuro nativo
- Fallback a FolderBrowserDialog estándar si falla
- Manejo automático de errores
- Restauración del modo claro después del diálogo

**Uso:**
```csharp
var path = DarkFolderBrowserDialog.ShowDialog(this, "Seleccionar carpeta de descargas");
if (!string.IsNullOrWhiteSpace(path))
{
    // Usar la ruta seleccionada
}
```

### 3. **DarkFileDialog** (`UI/DarkFileDialog.cs`)
Diálogos de apertura y guardado de archivos con modo oscuro.

**Características:**
- Soporte para OpenFileDialog y SaveFileDialog
- Modo oscuro mediante Windows API
- Filtros de archivo personalizables
- Directorio inicial configurable
- Nombre de archivo por defecto (SaveDialog)
- Soporte para multiselección (OpenDialog)

**Uso:**
```csharp
// Abrir archivo
var filePath = DarkFileDialog.ShowOpenDialog(
    this, 
    "Seleccionar archivo", 
    "Archivos de texto (*.txt)|*.txt|Todos (*.*)|*.*",
    initialDirectory: @"C:\Downloads"
);

// Guardar archivo
var savePath = DarkFileDialog.ShowSaveDialog(
    this,
    "Guardar como",
    "CSV files (*.csv)|*.csv",
    defaultFileName: "export.csv"
);
```

## Implementación Técnica

### Windows API Integration

El sistema utiliza funciones no documentadas de `uxtheme.dll` para habilitar el modo oscuro:

```csharp
[DllImport("uxtheme.dll", EntryPoint = "#135")]
private static extern void AllowDarkModeForApp(bool allow);

[DllImport("uxtheme.dll", EntryPoint = "#138")]
private static extern int SetPreferredAppMode(int mode);

[DllImport("uxtheme.dll", EntryPoint = "#133")]
private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);
```

### Paleta de Colores

| Elemento | Color | Código |
|----------|-------|--------|
| Fondo principal | Gris oscuro | `#1E1E1E` |
| Fondo secundario | Gris medio | `#2D2D30` |
| Fondo hover | Gris claro | `#3E3E42` |
| Texto principal | Blanco | `#FFFFFF` |
| Texto secundario | Gris claro | `#CCCCCC` |
| Bordes | Gris medio | `#3F3F46` |
| Acento (botones) | Azul oscuro | `#007ACC` |
| Acento hover | Azul claro | `#1C97EA` |

## Beneficios

1. **Reducción de Fatiga Visual**: Menor emisión de luz azul en entornos oscuros
2. **Consistencia**: Experiencia uniforme en toda la aplicación
3. **Modernidad**: Interfaz alineada con estándares actuales de diseño
4. **Accesibilidad**: Mejor contraste y legibilidad
5. **Integración Nativa**: Usa APIs de Windows para máxima compatibilidad

## Compatibilidad

- **Windows 10 (1809+)**: Soporte completo de modo oscuro
- **Windows 11**: Soporte completo con mejoras visuales
- **Windows 7/8**: Fallback a diálogos estándar (sin modo oscuro)

## Migración desde Diálogos Estándar

### Antes:
```csharp
using (var dialog = new FolderBrowserDialog())
{
    if (dialog.ShowDialog() == DialogResult.OK)
    {
        var path = dialog.SelectedPath;
    }
}
```

### Después:
```csharp
var path = DarkFolderBrowserDialog.ShowDialog(this, "Seleccionar carpeta");
if (!string.IsNullOrWhiteSpace(path))
{
    // Usar path
}
```

## Notas de Desarrollo

- Los diálogos oscuros restauran automáticamente el modo claro después de cerrarse
- El manejo de errores está integrado con mensajes al usuario
- No requiere dependencias externas
- Compatible con async/await
- Thread-safe para operaciones en UI

## Futuras Mejoras

- [ ] Soporte para temas personalizables
- [ ] Animaciones de transición
- [ ] Modo alto contraste
- [ ] Sincronización con tema del sistema
- [ ] Preferencias de usuario persistentes
