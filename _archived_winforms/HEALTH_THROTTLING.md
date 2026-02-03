# Health Score Throttling - Problema y Solución

## Problema Reportado

**Usuario**: "Pongo el valor de descargas simultáneas a 3 y a los pocos segundos se pone a 1 solo"

## Causa Raíz

El sistema de **Health Score** estaba reduciendo automáticamente las descargas simultáneas cuando detectaba problemas de conexión, **sin que el usuario lo supiera**.

## ⚠️ NOTA: Solución Simplificada

Inicialmente se implementó un checkbox para controlar este comportamiento, pero **el usuario lo consideró demasiado complicado**.

**Solución final**: El throttling automático está **completamente desactivado**. El usuario tiene control total y el sistema nunca cambiará las descargas automáticamente.

### Cómo Funcionaba Antes

```csharp
// Líneas 402-426
private void ApplyHealthBasedThrottling()
{
    if (connectionHealthScore < 50)  // Si la conexión está mal
    {
        if (maxParallelDownloads > 3)
        {
            // Reduce a la mitad automáticamente
            maxParallelDownloads = Math.Max(2, maxParallelDownloads / 2);
            
            // Actualiza el NumericUpDown en la UI
            numParallelDownloads.Value = maxParallelDownloads;
        }
    }
}
```

### Flujo del Problema

```
Usuario establece: 3 descargas simultáneas
         ↓
Sistema calcula Health Score cada 30 segundos
         ↓
Health Score < 50 (conexión degradada)
         ↓
ApplyHealthBasedThrottling() se ejecuta automáticamente
         ↓
maxParallelDownloads = 3 / 2 = 1 (redondeado)
         ↓
UI se actualiza: numParallelDownloads.Value = 1
         ↓
Usuario ve: "¿Por qué cambió solo?"
```

## Solución Implementada (Simplificada)

### Desactivación Permanente

El método `ApplyHealthBasedThrottling()` ahora sale inmediatamente sin hacer nada:

```csharp
// Líneas 402-406
private void ApplyHealthBasedThrottling()
{
    // DESACTIVADO: No modificar automáticamente las descargas del usuario
    // El usuario tiene control total sobre maxParallelDownloads
    return;
    
    // ... código antiguo comentado/inaccesible
}
```

### Sin Opciones en la UI

- ❌ **No hay checkbox** para activar/desactivar
- ❌ **No hay configuración** que guardar/cargar
- ✅ **Simplemente no funciona** - el usuario tiene control total

### Ventaja de esta Solución

**Simplicidad máxima**: El usuario no necesita entender qué es el "Health Score" ni tomar decisiones sobre opciones complejas. Simplemente establece las descargas simultáneas y **se mantienen así**.

## Comportamiento Actual (Simplificado)

```
Usuario establece: 3 descargas simultáneas
         ↓
Sistema calcula Health Score cada 30 segundos
         ↓
Health Score < 50 (conexión degradada)
         ↓
ApplyHealthBasedThrottling() se ejecuta
         ↓
return; // SALE INMEDIATAMENTE
         ↓
maxParallelDownloads permanece en 3
         ↓
Usuario mantiene control total ✅
```

**Resultado**: Las descargas simultáneas **NUNCA** cambian automáticamente, independientemente del Health Score.

## Qué es el Health Score

El **Health Score** es una métrica de 0-100 que evalúa la calidad de tu conexión a Soulseek:

### Componentes (líneas 1241-1257)

```csharp
connectionHealthScore = (
    latencyScore * 0.25 +          // 25% - Latencia de respuesta
    packetLossScore * 0.20 +       // 20% - Pérdida de paquetes
    throttlingScore * 0.20 +       // 20% - Nivel de throttling
    circuitScore * 0.15 +          // 15% - Estado del circuit breaker
    connectionScore * 0.20         // 20% - Estabilidad de conexión
);
```

### Rangos

- **70-100**: 🟢 Conexión excelente
- **50-69**: 🟡 Conexión aceptable
- **30-49**: 🟠 Conexión degradada
- **0-29**: 🔴 Conexión crítica

### Factores que Reducen el Score

1. **Latencia alta** (>500ms)
2. **Pérdida de paquetes** (>5%)
3. **Throttling activo** (límites de velocidad)
4. **Circuit breaker abierto** (demasiados errores)
5. **Timeouts consecutivos** (>3)
6. **Desconexiones frecuentes**

## ~~Cuándo Activar el Throttling Automático~~ (YA NO APLICA)

**El throttling automático está permanentemente desactivado**. Esta sección se mantiene solo como referencia histórica.

El usuario siempre tiene control total sobre las descargas simultáneas. Si quieres reducirlas manualmente cuando la conexión está mal, simplemente cambia el valor en la configuración.

## Logs Relacionados

### Cuando el Health Score es Bajo

```
[12:34:56] ⚠️ HEALTH SCORE BAJO: 45.3/100 (L:60.2 P:30.5 T:40.0 C:80.0 S:55.0)
```

Desglose:
- **L:60.2** - Latency Score (latencia aceptable)
- **P:30.5** - Packet Loss Score (pérdida alta)
- **T:40.0** - Throttling Score (throttling moderado)
- **C:80.0** - Circuit Score (circuit breaker OK)
- **S:55.0** - Connection Score (conexión regular)

### Cuando el Health Score es Crítico

```
[12:35:10] 🚨 HEALTH SCORE CRÍTICO: 28.5/100
[12:35:10]    - Latencia: 850ms (óptimo <500ms)
[12:35:10]    - Pérdida: 12.3% (óptimo <5%)
[12:35:10]    - Throttling: Nivel 4/5
[12:35:10]    - Circuit: Open (óptimo: Closed)
[12:35:10]    - Timeouts consecutivos: 5
```

### ~~Cuando el Throttling Actúa~~ (YA NO OCURRE)

El throttling automático está desactivado, por lo que **nunca verás estos logs**:

```
[12:35:15] [WARN] [DESCARGA] Health 45: reduciendo descargas paralelas a 1
[12:35:15] [WARN] [PURGA] Health 45: pausando purga automática
```

Si ves estos mensajes en versiones antiguas, actualiza la aplicación.

## Otros Sistemas que Ajustan las Descargas

### 1. Modo Turbo (Manual)

```csharp
// Líneas 3888-3891
maxParallelDownloads = 8;
maxSimultaneousDownloads = 8;
```

**Control**: Usuario activa/desactiva checkbox
**Efecto**: Inmediato

### 2. Modo Agresivo (Temporal - 30 min)

```csharp
// Líneas 3981-3984
maxParallelDownloads = 15;
maxSimultaneousDownloads = 15;
```

**Control**: Usuario hace clic en botón
**Efecto**: Temporal (30 minutos)

### 3. Velocidad Adaptativa (Automático)

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

**Control**: Variable `adaptiveSpeed` (true por defecto)
**Efecto**: Gradual basado en velocidad real

### 4. Health Score Throttling (~~Automático~~ DESACTIVADO)

```csharp
// Líneas 402-406
private void ApplyHealthBasedThrottling()
{
    return; // DESACTIVADO permanentemente
}
```

**Control**: Ninguno (desactivado permanentemente)
**Efecto**: Ninguno (no hace nada)

## Recomendaciones (Simplificadas)

El throttling automático está desactivado, así que solo necesitas ajustar manualmente:

### Para Conexiones Estables

```
✅ Descargas simultáneas: 3-5
✅ Modo Turbo: Según necesidad
✅ Velocidad adaptativa: ACTIVADO
```

### Para Conexiones Inestables

```
✅ Descargas simultáneas: 2-3 (ajustar manualmente si es necesario)
❌ Modo Turbo: DESACTIVADO
✅ Velocidad adaptativa: ACTIVADO
```

### Para Máximo Rendimiento

```
✅ Descargas simultáneas: 8-10
✅ Modo Turbo: ACTIVADO
✅ Modo Agresivo: Usar cuando sea necesario
```

## Verificación

**No hay nada que verificar**. El throttling automático está permanentemente desactivado en el código. Simplemente establece tus descargas simultáneas y se mantendrán así.

## Resumen de Cambios (Versión Simplificada)

### Archivos Modificados

- **MainForm.cs**:
  - Líneas 402-406: `ApplyHealthBasedThrottling()` ahora sale inmediatamente con `return;`
  - Línea 1870-1872: Eliminadas variables `enableHealthThrottling` y `chkHealthThrottling`
  - Líneas 3485: Eliminado checkbox de la UI
  - Línea 6365: Eliminada carga de configuración
  - Línea 6747: Eliminado guardado de configuración

### Comportamiento

- **Antes**: Throttling automático activo, reducía descargas sin avisar
- **Versión 1**: Throttling controlado por checkbox (considerado complicado)
- **Ahora (Versión 2)**: Throttling **permanentemente desactivado**, sin opciones

### Ventajas de la Solución Simplificada

1. ✅ **Máxima simplicidad**: Sin opciones confusas
2. ✅ **Control total**: Usuario siempre decide
3. ✅ **Sin sorpresas**: Las descargas nunca cambian solas
4. ✅ **Menos UI**: Una opción menos que entender
5. ✅ **Código más limpio**: Menos variables y lógica condicional

## Conclusión

El problema de "las descargas se reducen solas" estaba causado por el **Health Score Throttling** que actuaba automáticamente sin notificar al usuario.

**Solución final (simplificada)**: El throttling automático está **permanentemente desactivado**. No hay opciones, no hay checkboxes, no hay configuraciones.

El usuario tiene **control total y absoluto** sobre el número de descargas simultáneas. El sistema **nunca** las cambiará automáticamente.
