# Fix: Cola de Descargas No Descargaba

## Problema Identificado

La cola de descargas no procesaba archivos automáticamente porque **el `downloadManager` no se iniciaba** después de cargar la cola desde el archivo guardado.

## Causa Raíz

En el método `LoadDownloadQueue()` (línea 6363 de `MainForm.cs`):
- Se cargaban correctamente los archivos desde `cola_descargas.json`
- Se agregaban a la cola `downloadQueue`
- Pero **NO se iniciaba** el `downloadManager` para procesarlos

## Solución Implementada

### Cambio en `MainForm.cs` (líneas 6495-6502)

```csharp
Log($"✅ Cola cargada automáticamente: {added} archivo(s) añadido(s), {skipped} omitido(s) desde {filePath}");

// Iniciar el gestor de descargas si hay archivos en la cola
if (added > 0 && downloadManager != null)
{
    downloadManager.Start();
    Log("▶️ Gestor de descargas iniciado automáticamente");
}
```

## Cómo Funciona el Sistema de Descargas

### 1. **Estructura de la Cola**
- `downloadQueue`: Lista de tareas de descarga (`List<DownloadTask>`)
- `downloadManager`: Gestor que procesa la cola (`SlskDown.Core.DownloadManager`)
- `downloadQueueLock`: Lock para acceso thread-safe

### 2. **Flujo de Descarga**

```
AddToDownloadQueue() 
  ↓
downloadManager.AddToQueue()
  ↓
downloadManager.ProcessQueueLoop() [ejecuta cada 1 segundo]
  ↓
ProcessQueue()
  ↓
OnDownloadFile callback → DownloadFileFromTask()
  ↓
ProcessDownload()
  ↓
client.DownloadAsync() [descarga real]
```

### 3. **Inicio del Gestor**

El `downloadManager.Start()` debe llamarse en estos casos:
- ✅ Al cargar cola desde archivo (ahora corregido)
- ✅ Al reanudar descargas manualmente
- ✅ Al agregar archivos a la cola automáticamente
- ✅ Al reconectar después de desconexión

## Archivos Modificados

### `MainForm.cs`
- **Líneas 6495-6502**: Agregado inicio automático del gestor de descargas

### `MainForm.Downloads.cs` (archivo existente pero no usado)
- Este archivo tiene una implementación alternativa de cola que NO se usa
- El sistema real usa `downloadManager` de `Core/DownloadManager.cs`
- Se puede eliminar o refactorizar en el futuro

## Verificación

Para verificar que la cola funciona:

1. **Agregar archivos a la cola**:
   - Búsqueda automática
   - Búsqueda manual + "Agregar a cola"

2. **Verificar logs**:
   ```
   ✅ Cola cargada automáticamente: X archivo(s) añadido(s)
   ▶️ Gestor de descargas iniciado automáticamente
   ✅ Download Manager iniciado (guardado periódico cada 3 min)
   ```

3. **Verificar UI**:
   - Pestaña "Descargas" debe mostrar archivos
   - Estado debe cambiar de "En cola" → "Descargando" → "Completado"
   - Porcentaje debe actualizarse en tiempo real

## Mejoras Adicionales Recomendadas

1. **Eliminar `MainForm.Downloads.cs`**: No se usa y puede causar confusión
2. **Agregar botón "Iniciar/Pausar Cola"** en la UI
3. **Mostrar estado del gestor** (Running/Stopped) en la UI
4. **Agregar indicador visual** cuando la cola está procesando

## Notas Técnicas

- El `downloadManager` usa un loop asíncrono que procesa la cola cada 1 segundo
- Máximo 3 descargas simultáneas (configurable en `DownloadManagerConfig`)
- Soporta reintentos automáticos y búsqueda de fuentes alternativas
- Guarda estado cada 3 minutos (inspirado en Nicotine+)

## Estado

✅ **CORREGIDO** - La cola de descargas ahora inicia automáticamente al cargar archivos guardados
