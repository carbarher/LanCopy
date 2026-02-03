# 🚀 SlskDown - Búsqueda Automática de Autores

## 📋 Descripción

SlskDown incluye un sistema avanzado de **búsqueda automática de autores** que te permite buscar automáticamente obras de tus autores favoritos en Soulseek.

## ✅ Características Principales

### 📂 Gestión de Lista de Autores
- **Archivo**: `authors_list.txt` (un autor por línea)
- **Botones de gestión**:
  - 📂 **Cargar** - Importar lista desde archivo
  - 💾 **Guardar** - Exportar lista actual
  - ➕ **Agregar** - Añadir nuevo autor
  - ✏️ **Editar** - Modificar autor existente
  - ➖ **Eliminar** - Quitar autores seleccionados

### 🔄 Búsqueda Automática
- **Búsqueda periódica** por cada autor de la lista
- **Búsqueda en español** automática (agrega "español" al término)
- **Selección múltiple** de autores para buscar
- **Log en tiempo real** del progreso de búsqueda
- **Contador de pasadas vacías** para eliminar autores inactivos

### 📊 Funcionalidades Avanzadas
- **Auto-descarga configurable** de resultados
- **Filtros avanzados** (tamaño, extensión, bitrate)
- **Blacklist** de usuarios problemáticos
- **Historial** de descargas por autor
- **Estadísticas** de búsqueda

## 🎯 Cómo Usar

### 1. Ejecutar la Aplicación
```batch
c:\p2p\slskdown_autores.bat
```

### 2. Ir a la Pestaña "🔍 Búsqueda Automática"
- Verás la lista de autores cargada desde `authors_list.txt`
- Puedes agregar, editar o eliminar autores

### 3. Seleccionar Autores
- **Click simple**: Seleccionar un autor
- **Ctrl + Click**: Seleccionar múltiples autores
- **Botón "✅ Seleccionar Todos"**: Seleccionar toda la lista

### 4. Iniciar Búsqueda
- Click en **"🚀 Iniciar Búsqueda Ultra-Rápida"**
- El sistema buscará automáticamente por cada autor seleccionado
- Los resultados aparecerán en la pestaña "📊 Resultados"
- El log mostrará el progreso en tiempo real

### 5. Gestionar Resultados
- **Filtrar** por tamaño, extensión, bitrate
- **Descargar** archivos seleccionados
- **Ver historial** de descargas

## 📁 Archivos de Configuración

### `authors_list.txt`
Lista de autores (un autor por línea):
```
Gabriel García Márquez
Isabel Allende
Jorge Luis Borges
Julio Cortázar
Mario Vargas Llosa
```

### `config.json`
Configuración general:
```json
{
  "Username": "carbar",
  "Password": "Carlos66*",
  "DownloadDir": "c:\\p2p\\downloads"
}
```

### `downloaded_files.json`
Tracking de archivos descargados por autor

### `blacklist.json`
Usuarios bloqueados

## 🎨 Interfaz de Búsqueda Automática

```
┌─────────────────────────────────────────────────────────┐
│ 🔍 BÚSQUEDA AUTOMÁTICA DE AUTORES                       │
├─────────────────────────────────────────────────────────┤
│                                                          │
│ Lista de Autores:                                        │
│ ┌──────────────────┐  ┌─────────────────────────────┐  │
│ │ 📂 Cargar        │  │ Log de Búsqueda:            │  │
│ │ 💾 Guardar       │  │                             │  │
│ ├──────────────────┤  │ [Progreso en tiempo real]   │  │
│ │ Gabriel García   │  │                             │  │
│ │ Isabel Allende   │  │ ✅ Buscando: García Márquez │  │
│ │ Jorge Luis Borges│  │ 📊 50 resultados encontrados│  │
│ │ Julio Cortázar   │  │ ⬇️ Descargando 10 archivos  │  │
│ │ Mario Vargas     │  │                             │  │
│ └──────────────────┘  └─────────────────────────────┘  │
│                                                          │
│ ┌──────────────────────────────────────────────────┐   │
│ │ 🚀 Iniciar Búsqueda | 🗑️ Limpiar Log | ✅ Todos │   │
│ └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## 💡 Consejos

1. **Nombres completos**: Usa nombres completos de autores para mejores resultados
2. **Selección múltiple**: Busca varios autores a la vez para ahorrar tiempo
3. **Auto-descarga**: Configura límites razonables (5-10 archivos por autor)
4. **Blacklist**: Bloquea usuarios con archivos de mala calidad
5. **Historial**: Revisa el historial para evitar descargas duplicadas

## 🔧 Configuración Avanzada

### Auto-descarga
- Configurable de 1 a 20 archivos por búsqueda
- Ordena por tamaño (mejor calidad primero)
- Feedback visual detallado

### Filtros
- **Tamaño mínimo/máximo**: En MB
- **Extensión**: mp3, flac, epub, pdf, etc.
- **Bitrate mínimo**: Para audio de calidad
- **País**: Todos, España, Hispano

### Modo Incógnito
- No guarda historial de búsquedas
- No guarda historial de descargas
- Indicador visual en rojo

## 📊 Estadísticas

La aplicación mantiene estadísticas de:
- Descargas por autor
- Autores más buscados
- Formatos preferidos
- Velocidades de descarga
- Usuarios más activos

## 🛡️ Seguridad

- **Encriptación**: Credenciales encriptadas con DPAPI
- **Logs**: Actividad registrada en `logs/slskdown-YYYY-MM-DD.txt`
- **Blacklist**: Sistema de bloqueo de usuarios
- **Validación**: Verificación de archivos descargados

## 📝 Notas

- La búsqueda automática agrega "español" a cada autor automáticamente
- Los autores sin resultados por 3 búsquedas consecutivas se marcan como inactivos
- El log se limpia automáticamente después de 1000 líneas
- Los archivos de configuración se guardan automáticamente al cerrar

## 🚀 Versión

**SlskDown v4.0** - Búsqueda Automática de Autores
- 7,277 líneas de código
- 16 funcionalidades completas
- Optimizado para máximo rendimiento

---

**¡Disfruta de la búsqueda automática de tus autores favoritos!** 📚🎵
