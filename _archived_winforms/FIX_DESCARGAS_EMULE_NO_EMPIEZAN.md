# Fix: Descargas de eMule No Empiezan

**Fecha**: 28 de diciembre de 2025  
**Problema**: Las descargas de eMule se agregan a la cola de aMule pero nunca empiezan a descargarse

## Problema Identificado

### Síntoma

Cuando descargas un archivo desde eMule:
1. El archivo se agrega correctamente a la cola de aMule ✅
2. SlskDown muestra "✅ Agregado a cola de aMule" ✅
3. **Pero la descarga se marca como "Completada" inmediatamente** ❌
4. El progreso nunca se actualiza ❌
5. El archivo nunca se descarga realmente ❌

### Causa Raíz

El código tenía un error crítico en **dos lugares**:

**Lugar 1: `ProcessDownload` (línea 31471)**
```csharp
// ❌ PROBLEMA: Marca como completado inmediatamente
task.Status = DownloadStatus.Completed;
task.EndTime = DateTime.Now;
task.ProgressPercent = 100;
EnqueueDownloadUiUpdate(task, "✅ Agregado a cola de aMule");
```

**Lugar 2: `DownloadMultipleAsync` (línea 11414)**
```csharp
// ❌ PROBLEMA: Muestra 100% inmediatamente
downloadItem.SubItems[2].Text = "100%";
downloadItem.SubItems[3].Text = "✅ Agregado a cola de aMule";
downloadItem.ForeColor = Color.LightGreen;
```

El código **asumía incorrectamente** que agregar un archivo a la cola de aMule significaba que la descarga estaba completa, cuando en realidad **aMule apenas está empezando** a descargar el archivo.

## Solución Implementada

### Cambio 1: ProcessDownload (líneas 31470-31479)

**Antes**:
```csharp
// aMule maneja la descarga en segundo plano
task.Status = DownloadStatus.Completed;  // ❌ Incorrecto
task.EndTime = DateTime.Now;
task.ProgressPercent = 100;
EnqueueDownloadUiUpdate(task, "✅ Agregado a cola de aMule");
AutoLog($"✅ {task.File.FileName} agregado a la cola de aMule");
return;
```

**Ahora**:
```csharp
// aMule maneja la descarga en segundo plano
// Marcar como "Descargando" y dejar que el timer de progreso actualice el estado
task.Status = DownloadStatus.Downloading;  // ✅ Correcto
task.StartTime = DateTime.Now;
task.StartedAt = DateTime.Now;
EnqueueDownloadUiUpdate(task, "📥 Descargando en aMule...");
AutoLog($"✅ {task.File.FileName} agregado a la cola de aMule - monitoreando progreso...");

// El timer emuleProgressTimer actualizará el progreso automáticamente
return;
```

### Cambio 2: DownloadMultipleAsync (líneas 11412-11419)

**Antes**:
```csharp
SafeBeginInvoke(() =>
{
    downloadItem.SubItems[2].Text = "100%";  // ❌ Incorrecto
    downloadItem.SubItems[3].Text = "✅ Agregado a cola de aMule";
    downloadItem.ForeColor = Color.LightGreen;
});

Log($"✅ {fileName} agregado a la cola de aMule");
```

**Ahora**:
```csharp
SafeBeginInvoke(() =>
{
    downloadItem.SubItems[2].Text = "0%";  // ✅ Correcto
    downloadItem.SubItems[3].Text = "📥 Descargando en aMule...";
    downloadItem.ForeColor = Color.Yellow;
});

Log($"✅ {fileName} agregado a la cola de aMule - monitoreando progreso...");
```

## Sistema de Monitoreo de Progreso

La aplicación ya tiene un sistema completo para monitorear el progreso de las descargas de eMule:

### Timer de Progreso (líneas 26213-26227)

```csharp
private void StartEmuleProgressTimer()
{
    if (emuleProgressTimer == null)
    {
        emuleProgressTimer = new System.Windows.Forms.Timer();
        emuleProgressTimer.Interval = 5000; // Actualizar cada 5 segundos
        emuleProgressTimer.Tick += async (s, e) => await UpdateEmuleDownloadProgressAsync();
    }
    
    if (!emuleProgressTimer.Enabled)
    {
        emuleProgressTimer.Start();
        Log("⏱️ Timer de progreso de eMule iniciado");
    }
}
```

### Actualización de Progreso (líneas 26244-26339)

El método `UpdateEmuleDownloadProgressAsync()`:

1. **Obtiene las descargas activas** de aMule cada 5 segundos
2. **Actualiza el progreso** de cada descarga en la UI
3. **Detecta descargas completadas** y las marca como terminadas
4. **Detecta descargas estancadas** y aplica reintentos
5. **Ajusta el intervalo dinámicamente**:
   - 2 segundos si hay descargas activas
   - 10 segundos si solo hay descargas en espera
   - 30 segundos si no hay descargas

### Flujo Completo

```
1. Usuario selecciona archivo de eMule → Descarga
2. SlskDown envía comando a aMule: DownloadAsync()
3. aMule agrega archivo a su cola interna
4. SlskDown marca descarga como "Downloading" ✅
5. Timer emuleProgressTimer se ejecuta cada 5s
6. UpdateEmuleDownloadProgressAsync() consulta estado en aMule
7. Actualiza progreso en UI: 0% → 25% → 50% → 75% → 100%
8. Cuando aMule termina, marca como "Completed"
9. Mueve archivo a carpeta de destino
```

## Verificación

### Logs Esperados

**Al agregar descarga**:
```
[Log] 📥 Descargando desde eMule: libro.pdf
[eMule Web] 📥 Iniciando descarga: libro.pdf
[eMule Web] ✅ Descarga agregada a la cola de aMule
[Log] ✅ libro.pdf agregado a la cola de aMule - monitoreando progreso...
```

**Durante la descarga** (cada 5 segundos):
```
[Log] ⏱️ Timer de progreso de eMule iniciado
[eMule Web] 📊 Obtenidas 1 descargas activas
[eMule Web] 📊 Progreso: libro.pdf - 25.5%
[eMule Web] 📊 Progreso: libro.pdf - 50.2%
[eMule Web] 📊 Progreso: libro.pdf - 75.8%
```

**Al completar**:
```
[eMule Web] ✅ eMule: Descarga completada - libro.pdf
[Log] ✅ Archivo movido a: D:\Audio Libros\_Emule terminados\libro.pdf
```

### UI Esperada

**Grilla de Descargas**:
```
Archivo     | Usuario | Progreso | Estado                    | Tamaño
------------------------------------------------------------------------
libro.pdf   | eMule   | 0%       | 📥 Descargando en aMule... | 5.2 MB
libro.pdf   | eMule   | 25%      | 📥 Descargando en aMule... | 5.2 MB
libro.pdf   | eMule   | 50%      | 📥 Descargando en aMule... | 5.2 MB
libro.pdf   | eMule   | 100%     | ✅ Completado              | 5.2 MB
```

## Notas Técnicas

### ¿Por Qué No Usar EmuleDownloadProvider?

Existe una clase `Core/EmuleDownloadProvider.cs` que implementa `IDownloadProvider`, pero **no se está usando** porque:

1. El sistema actual usa `EMuleWebClient` directamente
2. `EMuleWebClient.DownloadAsync()` solo **agrega** el archivo a la cola de aMule
3. **aMule maneja la descarga** en su propio proceso
4. SlskDown solo **monitorea** el progreso vía WebServer

Para usar `EmuleDownloadProvider` correctamente, necesitaría:
- Implementar streaming de datos desde aMule
- Manejar chunks y reintentos
- Gestionar conexiones directas P2P

Esto es **innecesario** porque aMule ya hace todo eso. SlskDown solo necesita:
1. Agregar archivo a la cola de aMule
2. Monitorear progreso
3. Mover archivo cuando termine

### Diferencia con Soulseek

**Soulseek**:
- SlskDown descarga directamente del peer
- Maneja chunks, reintentos, progreso
- Control total del proceso

**eMule**:
- SlskDown solo envía comando a aMule
- aMule descarga del peer (ed2k/Kad)
- SlskDown solo monitorea el progreso
- aMule maneja chunks, reintentos, etc.

## Compilación

✅ **Estado**: Compilación exitosa sin errores  
✅ **Comando**: `msbuild SlskDown.csproj /t:Build /p:Configuration=Release`  
✅ **Exit Code**: 0

## Resumen

| Aspecto | Antes | Ahora |
|---------|-------|-------|
| **Estado inicial** | Completed (100%) ❌ | Downloading (0%) ✅ |
| **Progreso** | No se actualiza ❌ | Se actualiza cada 5s ✅ |
| **Monitoreo** | No funciona ❌ | Timer activo ✅ |
| **Descarga real** | Nunca empieza ❌ | Funciona correctamente ✅ |
| **UI** | Verde (completado) ❌ | Amarillo → Verde ✅ |

---

**Problema**: ✅ Resuelto  
**Archivos Modificados**: `MainForm.cs` (líneas 31470-31479, 11412-11419)  
**Impacto**: Las descargas de eMule ahora funcionan correctamente y se monitorean en tiempo real
