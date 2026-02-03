# Cambio de Descargas Simultáneas en Tiempo Real

## Pregunta

**¿Modificar el número de descargas simultáneas mientras hay cola de descargas en marcha hace efecto inmediato?**

## Respuesta: SÍ ✅

El cambio tiene **efecto prácticamente inmediato** (máximo 500ms de retraso).

## Cómo Funciona

### 1. Control en la UI

En la pestaña **Configuración**, el control `NumericUpDown` para "Simultáneas" actualiza dos variables cuando cambias el valor:

```csharp
// Líneas 3458-3471
numParallelDownloads.ValueChanged += (s, e) =>
{
    int oldValue = maxParallelDownloads;
    maxParallelDownloads = (int)numParallelDownloads.Value;      // Variable principal
    maxSimultaneousDownloads = (int)numParallelDownloads.Value;  // Variable usada por el manager
    
    // Log para confirmar el cambio
    LogEx(LogLevel.INFO, LogCategory.DESCARGA, 
        $"Descargas simultáneas: {oldValue} → {maxParallelDownloads} (efecto inmediato)");
    
    SaveConfig();  // Guarda en config.json
};
```

### 2. Download Manager Loop

El gestor de descargas se ejecuta en un loop continuo que verifica el límite cada **500 milisegundos**:

```csharp
// Líneas 20684-20872
private async Task StartDownloadManager()
{
    while (downloadManagerRunning)
    {
        lock (downloadQueueLock)
        {
            int activeDownloads = 0;
            
            // Cuenta descargas activas
            foreach (var task in downloadQueue)
            {
                if (task.Status == DownloadStatus.Downloading)
                    activeDownloads++;
            }
            
            // Calcula slots disponibles usando el valor actualizado
            var slotsAvailable = maxSimultaneousDownloads - activeDownloads;
            
            // Inicia nuevas descargas si hay slots disponibles
            tasksToProcess = pendingList.Take(slotsAvailable).ToArray();
        }
        
        // Procesa las tareas seleccionadas
        foreach (var task in tasksToProcess)
        {
            _ = Task.Run(async () => await ProcessDownload(task));
        }
        
        // Espera 500ms antes de la siguiente iteración
        await Task.Delay(500);
    }
}
```

### 3. Flujo de Ejecución

```
Usuario cambia valor en UI
         ↓
maxSimultaneousDownloads actualizado inmediatamente
         ↓
Download Manager loop (se ejecuta cada 500ms)
         ↓
Calcula: slotsAvailable = maxSimultaneousDownloads - activeDownloads
         ↓
Si aumentaste el límite:
  → Inicia nuevas descargas inmediatamente (si hay pendientes)
         ↓
Si redujiste el límite:
  → No inicia nuevas descargas hasta que activeDownloads < nuevo límite
  → Las descargas en curso NO se cancelan
```

## Ejemplos Prácticos

### Ejemplo 1: Aumentar de 3 a 5

**Estado inicial:**
- Límite: 3 descargas
- Activas: 3 descargas
- Pendientes: 10 archivos

**Cambias a 5:**
```
[Inmediato] maxSimultaneousDownloads = 5
[Log] "Descargas simultáneas: 3 → 5 (efecto inmediato)"

[Próxima iteración del loop, máximo 500ms después]
slotsAvailable = 5 - 3 = 2
→ Inicia 2 descargas más inmediatamente
→ Ahora hay 5 descargas activas
```

### Ejemplo 2: Reducir de 5 a 2

**Estado inicial:**
- Límite: 5 descargas
- Activas: 5 descargas
- Pendientes: 8 archivos

**Cambias a 2:**
```
[Inmediato] maxSimultaneousDownloads = 2
[Log] "Descargas simultáneas: 5 → 2 (efecto inmediato)"

[Las 5 descargas en curso continúan hasta completarse]

[Cuando una descarga termina]
Activas: 4
slotsAvailable = 2 - 4 = -2 (negativo)
→ NO inicia nuevas descargas

[Cuando otra descarga termina]
Activas: 3
slotsAvailable = 2 - 3 = -1 (negativo)
→ NO inicia nuevas descargas

[Cuando otra descarga termina]
Activas: 2
slotsAvailable = 2 - 2 = 0
→ NO inicia nuevas descargas (límite alcanzado)

[Cuando otra descarga termina]
Activas: 1
slotsAvailable = 2 - 1 = 1
→ Inicia 1 nueva descarga
→ Ahora hay 2 descargas activas (nuevo límite respetado)
```

## Comportamiento Importante

### ✅ Lo que SÍ sucede inmediatamente:

1. **Variable actualizada**: `maxSimultaneousDownloads` cambia al instante
2. **Log visible**: Aparece mensaje confirmando el cambio
3. **Config guardado**: Se guarda en `config.json`
4. **Nuevas descargas**: Si aumentas el límite, se inician nuevas descargas en máximo 500ms

### ⚠️ Lo que NO sucede:

1. **Descargas en curso NO se cancelan**: Si reduces el límite, las descargas activas continúan hasta completarse
2. **No hay cancelación forzada**: El sistema espera a que las descargas terminen naturalmente

### 📊 Delay máximo: 500ms

El loop del download manager se ejecuta cada 500ms, por lo que:
- **Mejor caso**: Cambio aplicado en 0ms (si justo está en la iteración del loop)
- **Peor caso**: Cambio aplicado en 500ms (si acabas de perder la iteración)
- **Promedio**: ~250ms

## Casos de Uso

### 1. Velocidad Lenta → Aumentar Descargas

Si las descargas van lentas porque tienes muchas fuentes disponibles:

```
Cambias de 3 → 8
→ En máximo 500ms, se inician 5 descargas más
→ Aprovechas mejor el ancho de banda
```

### 2. Saturación de Red → Reducir Descargas

Si tu red se satura con muchas descargas simultáneas:

```
Cambias de 8 → 3
→ Las 8 descargas actuales continúan
→ Cuando terminen, solo se mantendrán 3 activas
→ Gradualmente se reduce la carga de red
```

### 3. Modo Turbo Automático

El sistema puede ajustar automáticamente según la velocidad:

```csharp
// Líneas 24012-24037
if (averageDownloadSpeed > 5.0 && maxParallelDownloads < 6)
{
    maxParallelDownloads++;  // Aumenta si hay velocidad alta
}
else if (averageDownloadSpeed < 1.0 && maxParallelDownloads > 1)
{
    maxParallelDownloads--;  // Reduce si hay velocidad baja
}
```

## Integración con Otros Sistemas

### 1. Health Score

El sistema de salud de conexión puede reducir automáticamente las descargas:

```csharp
// Líneas 405-425
if (connectionHealthScore < 50 && maxParallelDownloads > 3)
{
    maxParallelDownloads = Math.Max(2, maxParallelDownloads / 2);
    LogEx(LogLevel.WARN, LogCategory.DESCARGA, 
        $"Health {connectionHealthScore:F0}: reduciendo descargas paralelas a {maxParallelDownloads}");
}
```

### 2. Modo Turbo

Activa automáticamente 8 descargas simultáneas:

```csharp
// Líneas 3885-3891
if (chkTurboMode.Checked)
{
    maxParallelDownloads = 8;
    maxSimultaneousDownloads = 8;
}
```

### 3. Modo Agresivo

Activa temporalmente 15 descargas simultáneas:

```csharp
// Líneas 3973-3984
if (!aggressiveModeActive)
{
    savedMaxParallelDownloads = maxParallelDownloads;
    maxParallelDownloads = 15;
    maxSimultaneousDownloads = 15;
}
```

## Verificación en Logs

Cuando cambias el valor, verás en el log:

```
[12:34:56] [INFO] [DESCARGA] Descargas simultáneas: 3 → 5 (efecto inmediato)
```

Esto confirma que:
1. ✅ El cambio se detectó
2. ✅ Las variables se actualizaron
3. ✅ El efecto será inmediato (máximo 500ms)

## Conclusión

**SÍ, el cambio tiene efecto inmediato** con las siguientes características:

- ⚡ **Delay máximo**: 500ms (tiempo del loop)
- 📈 **Aumentar límite**: Nuevas descargas se inician inmediatamente
- 📉 **Reducir límite**: Se respeta gradualmente (sin cancelar descargas en curso)
- 💾 **Persistencia**: Se guarda en config.json automáticamente
- 📊 **Visible**: Aparece log confirmando el cambio

El sistema está diseñado para ser **responsivo** y **seguro**, permitiendo ajustes en tiempo real sin interrumpir las descargas en curso.
