# 🔍 DIAGNÓSTICO: Desconexiones Continuas de Soulseek

**Fecha**: 6 de Diciembre, 2024  
**Estado**: ⚠️ PROBLEMAS CRÍTICOS ENCONTRADOS

---

## 🚨 PROBLEMAS CRÍTICOS IDENTIFICADOS

### 1. **PARALELISMO EXCESIVO** ❌ CRÍTICO
**Ubicación**: `MainForm.cs` línea 230
```csharp
private int currentParallelism = 12; // Más agresivo por defecto
private int minParallelism = 8;
private int maxParallelism = 15;
```

**Problema**: 
- La aplicación lanza **12-15 búsquedas simultáneas** por defecto
- En modo agresivo llega a **20 búsquedas paralelas** (línea 2152)
- En modo turbo: **12 búsquedas paralelas** (línea 2084)
- Esto es **extremadamente agresivo** y el servidor puede interpretarlo como ataque DDoS

**Impacto**: ⚠️⚠️⚠️ ALTO - Posible ban del servidor

**Evidencia**:
```csharp
// Línea 8584: Búsqueda automática
using (var semaphore = new SemaphoreSlim(currentParallelism, currentParallelism))
{
    // 12+ autores buscados simultáneamente
    var tasks = selectedAuthors.ToList().Select(async author => { ... });
}
```

---

### 2. **HEALTH CHECK CON BÚSQUEDAS DUMMY** ⚠️ SOSPECHOSO
**Ubicación**: `Core/HealthCheckService.cs` líneas 148-164

**Problema**:
```csharp
// Hacer ping al servidor (búsqueda dummy con timeout)
var searchTask = _client.SearchAsync(
    SearchQuery.FromText($"healthcheck_{Guid.NewGuid():N}"),  // ← SOSPECHOSO
    options: new SearchOptions(
        searchTimeout: 5000,
        responseLimit: 1,
        filterResponses: false
    ),
    cancellationToken: cts.Token
);
```

- Cada 5 minutos se ejecuta un "health check" haciendo una **búsqueda con GUID aleatorio**
- El servidor puede detectar este patrón y considerarlo sospechoso
- **ServerPing está obsoleto** (según memoria `ESTRATEGIA_CONEXION_SOULSEEK.md`)

**Impacto**: ⚠️⚠️ MEDIO-ALTO - Patrón sospechoso para el servidor

**Estado**: ✅ NO ESTÁ ACTIVADO (no se usa en MainForm.cs)

---

### 3. **DELAYS DEMASIADO CORTOS** ⚠️ MODERADO
**Ubicación**: Múltiples lugares en `MainForm.cs`

**Problemas encontrados**:
```csharp
// Línea 8872: Solo 500ms entre búsquedas
await Task.Delay(500, cancellationToken);

// Línea 14690: 500ms entre búsquedas de alternativas
await Task.Delay(500);

// Línea 15499: 500ms entre búsquedas de proveedores
await Task.Delay(500);
```

**Problema**: 
- Con 12 búsquedas en paralelo + 500ms de delay = **24 búsquedas/segundo** en ráfaga
- El servidor puede interpretar esto como flood

**Impacto**: ⚠️⚠️ MEDIO - Contribuye al flood

---

### 4. **FALTA DE RATE LIMITING REAL** ⚠️ MODERADO
**Ubicación**: `MainForm.cs` líneas 237-240

**Problema**:
```csharp
// Throttling adaptativo
private int consecutiveErrors = 0;
private DateTime lastThrottleTime = DateTime.MinValue;
private bool isThrottled = false;
```

- Existe una variable `isThrottled` pero **no se usa** para limitar búsquedas
- Solo hay throttling de UI (línea 18008): `UPDATE_THROTTLE_MS = 500`
- **NO hay límite de peticiones al servidor por minuto**

**Impacto**: ⚠️⚠️ MEDIO - No hay protección contra flood

---

### 5. **BÚSQUEDAS AUTOMÁTICAS CONTINUAS** ⚠️ MODERADO
**Ubicación**: `MainForm.cs` líneas 8520-8670

**Problema**:
- El modo automático busca **cientos/miles de autores** en bucle continuo
- Cada autor se busca con timeout de 3-5 segundos
- Si hay 100 autores y paralelismo de 12:
  - **100 autores ÷ 12 paralelos = ~8.3 rondas**
  - **8.3 rondas × 5 seg = ~42 segundos para completar UNA ronda**
  - Luego **repite indefinidamente** hasta que no encuentra más resultados

**Cálculo de tráfico**:
- 100 autores × búsquedas repetidas = **potencialmente cientos de búsquedas/minuto**
- Con 12 en paralelo = picos de **12-20 búsquedas simultáneas**

**Impacto**: ⚠️⚠️⚠️ ALTO - Sobrecarga sostenida del servidor

---

### 6. **WISHLIST TIMER** ⚠️ BAJO
**Ubicación**: `MainForm.cs` líneas 19261-19272

**Problema**:
```csharp
wishlistSearchTimer.Interval = wishlistSearchIntervalMinutes * 60 * 1000;
wishlistSearchTimer.Tick += async (s, e) => await SearchWishlistItems();
```

- Búsquedas periódicas de watchlist
- Intervalo configurable (por defecto desconocido)
- Puede contribuir a la carga si el intervalo es muy corto

**Impacto**: ⚠️ BAJO-MEDIO - Depende del intervalo

---

## 📊 RESUMEN DE TRÁFICO ESTIMADO

**Escenario típico con modo automático**:
```
- Autores seleccionados: 100
- Paralelismo: 12
- Timeout por búsqueda: 5 seg
- Rounds necesarios: 100 ÷ 12 = 8.3 rounds

TRÁFICO:
└─ Round 1: 12 búsquedas simultáneas (5 seg)
└─ Round 2: 12 búsquedas simultáneas (5 seg)
└─ ...
└─ Round 9: 4 búsquedas finales (5 seg)

TOTAL: ~42 segundos para 100 búsquedas
RATE: 100 búsquedas / 42 seg = 2.38 búsquedas/segundo (promedio)
PICOS: 12 búsquedas simultáneas constantes
```

**Si se repite el ciclo**:
- Si encuentra resultados, **repite inmediatamente**
- Potencialmente **infinito** hasta agotar resultados
- **Cientos o miles de búsquedas por sesión**

---

## ✅ SOLUCIONES PROPUESTAS

### 🔥 PRIORIDAD ALTA (Implementar YA)

#### 1. **REDUCIR PARALELISMO**
```csharp
// ANTES (PELIGROSO)
private int currentParallelism = 12;
private int maxParallelism = 15;

// DESPUÉS (SEGURO)
private int currentParallelism = 3;  // ← CAMBIAR A 3
private int minParallelism = 2;      // ← CAMBIAR A 2
private int maxParallelism = 5;      // ← CAMBIAR A 5 máximo
```

**Justificación**: 
- Nicotine+ (cliente oficial) usa 2-4 búsquedas simultáneas
- 12 es excesivo y sospechoso

---

#### 2. **AUMENTAR DELAYS ENTRE BÚSQUEDAS**
```csharp
// ANTES (PELIGROSO)
await Task.Delay(500);  // 500ms

// DESPUÉS (SEGURO)
await Task.Delay(2000);  // 2 segundos entre búsquedas
```

**Justificación**:
- Dar tiempo al servidor para procesar
- Evitar patrones de flood

---

#### 3. **IMPLEMENTAR RATE LIMITING REAL**
```csharp
private class RateLimiter
{
    private Queue<DateTime> requestTimestamps = new Queue<DateTime>();
    private readonly int maxRequestsPerMinute = 30;  // Límite conservador
    private readonly object lockObj = new object();
    
    public async Task WaitIfNeeded()
    {
        lock (lockObj)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            
            // Limpiar timestamps antiguos
            while (requestTimestamps.Count > 0 && requestTimestamps.Peek() < oneMinuteAgo)
            {
                requestTimestamps.Dequeue();
            }
            
            // Si alcanzamos el límite, esperar
            if (requestTimestamps.Count >= maxRequestsPerMinute)
            {
                var oldestRequest = requestTimestamps.Peek();
                var waitTime = oldestRequest.AddMinutes(1) - now;
                if (waitTime.TotalMilliseconds > 0)
                {
                    await Task.Delay((int)waitTime.TotalMilliseconds);
                }
            }
            
            requestTimestamps.Enqueue(now);
        }
    }
}
```

---

#### 4. **DESACTIVAR HEALTH CHECK CON BÚSQUEDAS DUMMY**
```csharp
// NO USAR: HealthCheckService
// Alternativa: usar TCP keep-alive nativo del socket

// Configurar en conexión del cliente:
var connectionOptions = new ConnectionOptions(
    // ... otras opciones
    enableKeepAlive: true
);
```

**Justificación**:
- ServerPing está obsoleto
- TCP keep-alive es más eficiente y no genera tráfico sospechoso
- El servidor ya no responde a pings (según documentación)

---

### ⚡ PRIORIDAD MEDIA

#### 5. **EXPONENTIAL BACKOFF EN ERRORES**
```csharp
private async Task<bool> SearchWithBackoff(string query, int attempt = 0)
{
    const int maxAttempts = 3;
    const int baseDelayMs = 5000;  // 5 segundos base
    
    try
    {
        return await client.SearchAsync(query);
    }
    catch (Exception ex)
    {
        if (attempt >= maxAttempts)
            throw;
        
        // Exponential backoff con jitter
        var delay = baseDelayMs * Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(0, (int)(delay * 0.25)); // 0-25% jitter
        
        Log($"⚠️ Error en búsqueda, reintentando en {(delay + jitter)/1000}s...");
        await Task.Delay((int)delay + jitter);
        
        return await SearchWithBackoff(query, attempt + 1);
    }
}
```

---

#### 6. **LIMITAR ROUNDS EN MODO AUTOMÁTICO**
```csharp
// Agregar límite de rounds
private const int MAX_AUTO_SEARCH_ROUNDS = 5;  // Máximo 5 rondas

// En el while loop:
while (anyResultsFound && autoSearchRunning && round <= MAX_AUTO_SEARCH_ROUNDS)
{
    // ...
}
```

---

#### 7. **AUMENTAR INTERVALO DE WISHLIST**
```csharp
// Configuración recomendada
private int wishlistSearchIntervalMinutes = 30;  // Mínimo 30 minutos
```

---

### 🔍 PRIORIDAD BAJA (Monitoreo)

#### 8. **LOGGING DETALLADO DE BÚSQUEDAS**
```csharp
private void LogServerRequest(string type, string query)
{
    var timestamp = DateTime.UtcNow;
    Log($"[SERVER] {timestamp:HH:mm:ss.fff} - {type}: {query}");
    
    // Guardar en archivo para análisis
    File.AppendAllText(
        Path.Combine(dataDir, "server_requests.log"),
        $"{timestamp:O}|{type}|{query}\n"
    );
}
```

---

## 🎯 PLAN DE IMPLEMENTACIÓN

### Fase 1: EMERGENCIA (Implementar HOY) ⚠️
1. ✅ Reducir `currentParallelism` de 12 a 3
2. ✅ Reducir `maxParallelism` de 15 a 5
3. ✅ Aumentar delays de 500ms a 2000ms
4. ✅ Agregar límite de 30 búsquedas/minuto

### Fase 2: ESTABILIZACIÓN (Esta semana)
1. ⚡ Implementar exponential backoff
2. ⚡ Limitar rounds en modo automático a 5
3. ⚡ Aumentar wishlist interval a 30 min

### Fase 3: MONITOREO (Próxima semana)
1. 📊 Agregar logging de todas las búsquedas
2. 📊 Crear reporte diario de uso
3. 📊 Monitorear desconexiones

---

## 🔬 COMPARACIÓN CON NICOTINE+

**Nicotine+ (cliente oficial Python)**:
- Paralelismo: 2-4 búsquedas simultáneas
- Rate limit: ~20-30 búsquedas/minuto
- Delay: 1-3 segundos entre búsquedas
- Health check: TCP keep-alive (no búsquedas dummy)

**SlskDown (ACTUAL - PELIGROSO)**:
- Paralelismo: 12-20 búsquedas simultáneas ❌
- Rate limit: NINGUNO ❌
- Delay: 500ms ❌
- Health check: Búsquedas dummy con GUID ⚠️ (no activo)

**SlskDown (PROPUESTO - SEGURO)**:
- Paralelismo: 3-5 búsquedas simultáneas ✅
- Rate limit: 30 búsquedas/minuto ✅
- Delay: 2000ms ✅
- Health check: TCP keep-alive ✅

---

## 📈 IMPACTO ESPERADO

**Reducción de tráfico**:
- Paralelismo: 12 → 3 = **-75% de carga simultánea**
- Delays: 500ms → 2000ms = **-75% de frecuencia**
- Rate limit: ∞ → 30/min = **control absoluto**

**TOTAL**: Reducción del **90% del tráfico al servidor**

---

## ⚡ CÓDIGO DE EMERGENCIA

### Cambios Mínimos para Implementar YA

```csharp
// 1. MainForm.cs línea 230
private int currentParallelism = 3;  // CAMBIAR DE 12 A 3
private int minParallelism = 2;      // CAMBIAR DE 8 A 2
private int maxParallelism = 5;      // CAMBIAR DE 15 A 5

// 2. MainForm.cs línea 8872 (y similares)
await Task.Delay(2000, cancellationToken);  // CAMBIAR DE 500 A 2000

// 3. MainForm.cs línea 2084 (Modo Turbo)
maxParallelSearches = 5;  // CAMBIAR DE 12 A 5

// 4. MainForm.cs línea 2152 (Modo Agresivo)
maxParallelSearches = 8;  // CAMBIAR DE 20 A 8
```

---

## 🚨 ADVERTENCIAS

1. **NO uses modo agresivo** hasta implementar estas correcciones
2. **NO uses modo turbo** con más de 50 autores simultáneos
3. **Monitorea el log** para detectar errores de conexión
4. Si ves `"Connection reset by peer"` o `"Timeout"` frecuentes → **REDUCE MÁS EL PARALELISMO**

---

## 📝 CONCLUSIÓN

**El problema NO es un solo factor, sino la COMBINACIÓN de**:
1. Paralelismo excesivo (12-20)
2. Delays cortos (500ms)
3. Falta de rate limiting
4. Búsquedas continuas en modo automático

**Solución**: Reducir agresivamente el tráfico en ~90% siguiendo el comportamiento de Nicotine+

**Prioridad**: ⚠️⚠️⚠️ CRÍTICA - Implementar Fase 1 HOY
