# Sistema de Logging Mejorado

## Descripción General

El sistema de logging ha sido mejorado para ser más comprensible y filtrable, con niveles de severidad y categorías claras.

## Formato de Log

Cada línea de log sigue este formato estructurado:

```
[HH:mm:ss] 🔍 NIVEL   | CATEGORÍA  | Mensaje
```

### Ejemplo:
```
[10:30:45] ⚠️  WARN    | CONEXION   | Pausa anti-bloqueo: strike 2, 45 min. Razón: timeout
[10:31:12] ✅ SUCCESS  | DESCARGA   | Archivo completado: libro.epub (2.5 MB)
[10:31:45] ℹ️  INFO    | STEALTH    | Puerto rotado: 50000 → 50123
```

## Niveles de Log

| Nivel   | Icono | Descripción                                    | Uso                                  |
|---------|-------|------------------------------------------------|--------------------------------------|
| DEBUG   | 🔍    | Información detallada para debugging           | Desarrollo y diagnóstico             |
| INFO    | ℹ️     | Información general de operaciones             | Eventos normales del sistema         |
| WARN    | ⚠️     | Advertencias que requieren atención            | Problemas potenciales                |
| ERROR   | ❌    | Errores que afectan funcionalidad              | Fallos en operaciones                |
| SUCCESS | ✅    | Confirmación de operaciones exitosas           | Completación de tareas importantes   |

## Categorías

| Categoría  | Descripción                                    | Ejemplos                             |
|------------|------------------------------------------------|--------------------------------------|
| CONEXION   | Eventos de conexión/desconexión al servidor   | Reconexiones, timeouts, health score |
| DESCARGA   | Operaciones de descarga de archivos            | Inicio, progreso, completación       |
| BUSQUEDA   | Búsquedas de archivos                          | Inicio, resultados, filtros          |
| PURGA      | Proceso de purga automática                    | Inicio, pausa, eliminaciones         |
| STEALTH    | Modo stealth y anti-detección                  | Rotación de puertos, delays          |
| COMPARTIR  | Sistema de compartir archivos (uploads)        | Enqueue, browse, transferencias      |
| SISTEMA    | Configuración y eventos del sistema            | Inicio, guardado de config, errores  |
| GENERAL    | Otros eventos no categorizados                 | Mensajes generales                   |

## Filtros en la UI

En la pestaña "Log" encontrarás dos filtros:

### Filtro de Nivel
- **TODOS**: Muestra todos los niveles
- **DEBUG**: Solo mensajes de debugging
- **INFO**: Solo información general
- **WARN**: Solo advertencias
- **ERROR**: Solo errores
- **SUCCESS**: Solo confirmaciones de éxito

### Filtro de Categoría
- **TODAS**: Muestra todas las categorías
- **CONEXION**: Solo eventos de conexión
- **DESCARGA**: Solo eventos de descarga
- **BUSQUEDA**: Solo eventos de búsqueda
- **PURGA**: Solo eventos de purga
- **STEALTH**: Solo eventos de stealth mode
- **COMPARTIR**: Solo eventos de compartir archivos
- **SISTEMA**: Solo eventos del sistema
- **GENERAL**: Solo eventos generales

## Uso para Desarrolladores

### Método Recomendado: LogEx()

```csharp
// Sintaxis
LogEx(LogLevel nivel, LogCategory categoría, string mensaje);

// Ejemplos
LogEx(LogLevel.INFO, LogCategory.DESCARGA, "Iniciando descarga de archivo.epub");
LogEx(LogLevel.ERROR, LogCategory.CONEXION, $"Timeout al conectar: {ex.Message}");
LogEx(LogLevel.SUCCESS, LogCategory.PURGA, "Purga completada: 150 archivos eliminados");
LogEx(LogLevel.WARN, LogCategory.STEALTH, "Detectados intervalos cortos, aumentando delay");
```

### Método Legacy: Log()

El método `Log()` original sigue funcionando para compatibilidad. Detecta automáticamente el nivel y categoría basándose en palabras clave y emojis en el mensaje.

```csharp
// Aún funciona, pero menos preciso
Log("❌ Error al descargar archivo");
Log("✅ Conexión establecida correctamente");
```

## Ventajas del Nuevo Sistema

1. **Claridad**: Formato estructurado y consistente
2. **Filtrado**: Buscar problemas específicos rápidamente
3. **Diagnóstico**: Identificar categorías problemáticas
4. **Profesional**: Logs más legibles y organizados
5. **Compatibilidad**: El código legacy sigue funcionando

## Logs Resaltados de Reconexión

Cuando la aplicación se reconecta tras una desconexión, las tareas que se reanudan se muestran con un formato especial para facilitar su identificación:

```
[10:45:30] ✅ SUCCESS  | SISTEMA    | ═══════════════════════════════════════════════════
[10:45:30] ✅ SUCCESS  | SISTEMA    | ▶️  REANUDANDO PROCESOS TRAS RECONEXIÓN EXITOSA
[10:45:30] ✅ SUCCESS  | SISTEMA    | ═══════════════════════════════════════════════════
[10:45:30] ✅ SUCCESS  | BUSQUEDA   | ▶️  [REANUDADA] Búsqueda automática
[10:45:30] ✅ SUCCESS  | PURGA      | ▶️  [REANUDADA] Purga automática
[10:45:30] ✅ SUCCESS  | DESCARGA   | ▶️  [REANUDADAS] Descargas (15 pendientes en cola)
[10:45:30] ✅ SUCCESS  | SISTEMA    | ═══════════════════════════════════════════════════
[10:45:30] ✅ SUCCESS  | SISTEMA    | ✅ Total de procesos reanudados: 3
[10:45:30] ✅ SUCCESS  | SISTEMA    | ═══════════════════════════════════════════════════
```

### Tareas que se Pausan/Reanudan Automáticamente

1. **Búsqueda Automática**: Se pausa cuando se pierde la conexión y se reanuda tras reconectar
2. **Purga Automática**: Se pausa y reinicia desde el principio tras reconexión
3. **Descargas Activas**: Se pausan y reanudan, mostrando cuántas están pendientes

### Logs de Pausa por Desconexión

```
[10:40:15] ⚠️  WARN    | BUSQUEDA   | ⏸️  [PAUSADA] Búsqueda automática por desconexión
[10:40:15] ⚠️  WARN    | PURGA      | ⏸️  [PAUSADA] Purga automática por desconexión
[10:40:15] ⚠️  WARN    | DESCARGA   | ⏸️  [PAUSADAS] 5 descargas activas por desconexión
```

### Logs de Reintento de Operaciones

Cuando una operación falla y se reintenta tras reconexión:

```
[10:42:10] ⚠️  WARN    | CONEXION   | Error en operación 'search': Connection lost
[10:42:10] ℹ️  INFO    | CONEXION   | Intentando reconectar para reintentar operación...
[10:42:45] ✅ SUCCESS  | CONEXION   | 🔁 [REINTENTANDO] Operación 'search' tras reconexión exitosa
```

## Recomendaciones

- Usa **INFO** para eventos normales del flujo de trabajo
- Usa **WARN** para situaciones que podrían causar problemas
- Usa **ERROR** solo para fallos reales que afectan funcionalidad
- Usa **SUCCESS** para confirmar completación de tareas importantes
- Usa **DEBUG** solo durante desarrollo (se puede filtrar en producción)
- Usa el formato **[ACCIÓN]** entre corchetes para resaltar estados especiales (PAUSADA, REANUDADA, REINICIANDO, REINTENTANDO)

## Migración Gradual

El sistema es compatible con el código existente. Los nuevos logs deberían usar `LogEx()`, pero los logs legacy con `Log()` seguirán funcionando y se categorizarán automáticamente.
