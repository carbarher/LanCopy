# 🤖 Modo Automático - Simplificación UI

## Resumen

El **Modo Automático** es una nueva funcionalidad que simplifica la interfaz de usuario eliminando botones redundantes y automatizando tareas de mantenimiento comunes. Está activado por defecto y puede desactivarse desde la pestaña de Configuración.

## Botones Eliminados

Los siguientes botones fueron eliminados de la pestaña de Configuración porque sus funcionalidades ahora se ejecutan automáticamente:

### 1. 🔌 Test Conexión
- **Razón**: Auto-test cada 5 minutos con Modo Automático
- **Funcionalidad**: El sistema ahora verifica automáticamente la conexión cada 5 minutos cuando no hay actividad reciente
- **Ubicación anterior**: Configuración → Herramientas

### 2. 🗑️ Borrar cachés
- **Razón**: Auto-limpieza con Modo Automático
- **Funcionalidad**: Las cachés antiguas (>30 días) se eliminan automáticamente cada hora
- **Ubicación anterior**: Configuración → Herramientas

### 3. 🧹 Limpiar Duplicados
- **Razón**: Auto-detección con Rust + Modo Automático
- **Funcionalidad**: Los duplicados se detectan automáticamente antes de descargar usando el algoritmo de Levenshtein implementado en Rust
- **Ubicación anterior**: Configuración → Herramientas

### 4. 💾 Ver Backups
- **Razón**: Acceso desde explorador (carpeta data/backups)
- **Funcionalidad**: Los backups se pueden acceder directamente desde el explorador de archivos
- **Ubicación anterior**: Configuración → Herramientas

## Funcionalidades Automáticas

### 🔄 Auto-limpieza de resultados
- **Cuándo**: Al añadir archivos a la cola de descargas
- **Qué hace**: Si hay más de 100 resultados de búsqueda, los limpia automáticamente para liberar memoria
- **Beneficio**: Reduce el uso de memoria y mantiene la UI limpia

### 🗑️ Auto-limpieza de cachés
- **Cuándo**: Cada hora (timer automático)
- **Qué hace**: Elimina cachés de búsqueda, metadatos y proveedores que tengan más de 30 días
- **Beneficio**: Libera espacio en disco y mantiene las cachés actualizadas

### ▶️ Auto-inicio de cola
- **Cuándo**: Al conectar al servidor de Soulseek
- **Qué hace**: Reanuda automáticamente todas las descargas pausadas
- **Beneficio**: No es necesario hacer clic en "Iniciar Cola" manualmente

### 🔍 Auto-detección de duplicados
- **Cuándo**: Antes de descargar un archivo
- **Qué hace**: Usa el algoritmo de Levenshtein (implementado en Rust) para detectar archivos similares (>85% de similitud)
- **Beneficio**: Evita descargas duplicadas y ahorra ancho de banda

### 🔌 Auto-test de conexión
- **Cuándo**: Cada 5 minutos (timer automático)
- **Qué hace**: Verifica la conexión al servidor si no ha habido actividad reciente
- **Beneficio**: Detecta problemas de conexión automáticamente

## Checkbox de Configuración

### 🤖 Modo Automático
- **Ubicación**: Configuración → Opciones Generales
- **Estado por defecto**: Activado
- **Color**: Verde claro (para destacar)
- **Fuente**: Negrita

**Descripción completa**: 
```
🤖 Modo Automático (limpieza, optimización y detección auto)
```

**Logs al activar**:
```
🤖 Modo Automático ACTIVADO:
  • Auto-limpieza de resultados al añadir a descargas
  • Auto-limpieza de cachés viejas (>30 días)
  • Auto-inicio de cola al conectar
  • Auto-detección de duplicados con Rust
  • Auto-test de conexión cada 5 minutos
```

**Logs al desactivar**:
```
🤖 Modo Automático DESACTIVADO
```

## Implementación Técnica

### Variables de Clase
```csharp
private bool autoMode = true; // Activado por defecto
private CheckBox chkAutoMode;
private System.Windows.Forms.Timer autoCleanupTimer;
private System.Windows.Forms.Timer autoTestTimer;
```

### Métodos Principales

#### `InitializeAutoMode()`
- Inicializa los timers para limpieza y test de conexión
- Ejecuta limpieza inicial de cachés
- Auto-inicia la cola si está conectado

#### `StopAutoMode()`
- Detiene y libera los timers

#### `AutoCleanOldCaches()`
- Elimina cachés antiguas (>30 días)
- Archivos afectados: `search_cache.json`, `metadata_cache.json`, `provider_cache.json`

#### `AutoTestConnection()`
- Verifica conexión cada 5 minutos si no hay actividad

#### `AutoStartQueue()`
- Reanuda descargas pausadas al conectar

#### `AutoCleanSearchResults()`
- Limpia resultados de búsqueda si hay >100 items

#### `AutoDetectDuplicate(string fileName, string downloadPath)`
- Detecta duplicados usando Rust (Levenshtein distance)
- Umbral de similitud: 85%
- Muestra diálogo de confirmación si encuentra duplicados

## Persistencia

La configuración del Modo Automático se guarda en `config.json`:

```json
{
  "autoMode": true,
  ...
}
```

## Beneficios

1. **Simplificación UI**: Menos botones = interfaz más limpia
2. **Automatización**: Tareas de mantenimiento sin intervención manual
3. **Rendimiento**: Limpieza automática de memoria y disco
4. **Experiencia de usuario**: Menos clics, más productividad
5. **Integración Rust**: Detección de duplicados ultra-rápida

## Compatibilidad

- ✅ Compatible con todas las funcionalidades existentes
- ✅ Puede desactivarse si se prefiere control manual
- ✅ No afecta el rendimiento cuando está desactivado
- ✅ Configuración persistente entre sesiones

## Próximos Pasos

- [ ] Trackear última actividad para mejorar auto-test
- [ ] Añadir más métricas de auto-limpieza
- [ ] Configurar umbrales personalizables (días de caché, límite de resultados, etc.)
- [ ] Dashboard de estadísticas del Modo Automático

---

**Fecha de implementación**: 2025-01-XX  
**Versión**: 1.0  
**Estado**: ✅ Completado y compilado
