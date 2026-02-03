# Estrategia Óptima de Conexión y Reconexión para Soulseek

## Resumen Ejecutivo

Basado en investigación exhaustiva del protocolo Soulseek, implementaciones de referencia (Nicotine+), y mejores prácticas de resiliencia de red, esta es la estrategia óptima para mantener conexiones estables.

---

## 1. TCP Keep-Alive (Crítico)

### Problema
Las conexiones TCP pueden quedar en estado "half-open" cuando:
- El cliente/servidor crashea sin enviar FIN
- Fallo de red (router, cable, VPN)
- Firewall/NAT cierra la conexión silenciosamente

### Solución: TCP Keep-Alive Nativo

**Configuración Óptima para Soulseek:**
```csharp
socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

// Configurar intervalos personalizados (Windows)
// Estructura: [onoff, keepalivetime, keepaliveinterval]
byte[] keepAliveValues = new byte[12];
Buffer.BlockCopy(BitConverter.GetBytes(1u), 0, keepAliveValues, 0, 4);      // on
Buffer.BlockCopy(BitConverter.GetBytes(60000u), 0, keepAliveValues, 4, 4);  // 60s tiempo
Buffer.BlockCopy(BitConverter.GetBytes(10000u), 0, keepAliveValues, 8, 4);  // 10s intervalo

socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
```

**Valores Recomendados:**
- **keepalivetime**: 60 segundos (tiempo antes del primer keep-alive)
- **keepaliveinterval**: 10 segundos (intervalo entre probes)
- **keepaliveprobes**: 10 (fijo en Windows, no configurable)

**Detección de fallo**: ~60s + (10s × 10) = **160 segundos máximo**

### Alternativa: ServerPing del Protocolo Soulseek

Según documentación de Nicotine+:
- **Mensaje**: Server Code 32 (ServerPing)
- **Frecuencia**: Máximo 1 vez por minuto
- **Nota**: El servidor ya NO responde a estos mensajes
- **Recomendación**: Usar TCP keepalive en su lugar (más eficiente)

---

## 2. Exponential Backoff con Jitter

### Algoritmo Base

```
delay = min(maxDelay, baseDelay × (multiplier ^ attemptNumber))
actualDelay = delay + random(0, jitter)
```

### Configuración Óptima para Soulseek

**Escenario 1: Errores de Red/Timeout**
```
Intento 1: 5s  + jitter(0-2s)   = 5-7s
Intento 2: 10s + jitter(0-3s)   = 10-13s
Intento 3: 20s + jitter(0-5s)   = 20-25s
Intento 4: 40s + jitter(0-10s)  = 40-50s
Intento 5+: 60s + jitter(0-15s) = 60-75s (cap)
```

**Escenario 2: Errores de Autenticación**
```
Intento 1: 30s
Intento 2: 60s
Intento 3: 120s (2 min)
Intento 4: 300s (5 min)
Intento 5+: 600s (10 min) - considerar deshabilitar auto-reconexión
```

**Escenario 3: Posible Ban/Rate Limiting**
```
Strike 1: 5 minutos
Strike 2: 15 minutos
Strike 3: 30 minutos
Strike 4+: 60 minutos
```

### Implementación Recomendada

```csharp
public class ExponentialBackoff
{
    private int attemptCount = 0;
    private DateTime lastAttempt = DateTime.MinValue;
    private readonly Random random = new Random();
    
    public TimeSpan GetNextDelay(BackoffScenario scenario)
    {
        attemptCount++;
        
        int baseSeconds = scenario switch
        {
            BackoffScenario.NetworkError => GetNetworkErrorDelay(attemptCount),
            BackoffScenario.AuthError => GetAuthErrorDelay(attemptCount),
            BackoffScenario.RateLimiting => GetRateLimitDelay(attemptCount),
            _ => 30
        };
        
        // Jitter: 0-25% del delay base
        int jitterMs = random.Next(0, (baseSeconds * 250));
        
        return TimeSpan.FromSeconds(baseSeconds).Add(TimeSpan.FromMilliseconds(jitterMs));
    }
    
    private int GetNetworkErrorDelay(int attempt)
    {
        return attempt switch
        {
            1 => 5,
            2 => 10,
            3 => 20,
            4 => 40,
            _ => 60  // Cap at 60s
        };
    }
    
    private int GetAuthErrorDelay(int attempt)
    {
        return attempt switch
        {
            1 => 30,
            2 => 60,
            3 => 120,
            4 => 300,
            _ => 600  // Cap at 10 min
        };
    }
    
    private int GetRateLimitDelay(int strikes)
    {
        return strikes switch
        {
            1 => 300,   // 5 min
            2 => 900,   // 15 min
            3 => 1800,  // 30 min
            _ => 3600   // 60 min
        };
    }
    
    public void Reset()
    {
        attemptCount = 0;
        lastAttempt = DateTime.MinValue;
    }
}

public enum BackoffScenario
{
    NetworkError,
    AuthError,
    RateLimiting
}
```

---

## 3. Detección Inteligente de Desconexión

### Señales de Desconexión

**Inmediatas (actuar de inmediato):**
1. `client.State == SoulseekClientStates.Disconnected`
2. `SocketException` en operaciones
3. `TimeoutException` en operaciones críticas
4. Evento `client.Disconnected`

**Progresivas (monitorear):**
1. Keep-alive failures consecutivos (3+)
2. Timeouts en operaciones no críticas
3. Latencia creciente (>5s para operaciones simples)

### Monitor de Salud de Conexión

```csharp
public class ConnectionHealthMonitor
{
    private int consecutiveTimeouts = 0;
    private int consecutiveKeepAliveFailures = 0;
    private Queue<TimeSpan> latencySamples = new Queue<TimeSpan>(10);
    
    public ConnectionHealth CheckHealth()
    {
        if (consecutiveTimeouts >= 3)
            return ConnectionHealth.Critical;
        
        if (consecutiveKeepAliveFailures >= 3)
            return ConnectionHealth.Degraded;
        
        if (latencySamples.Count >= 5)
        {
            var avgLatency = TimeSpan.FromMilliseconds(
                latencySamples.Average(ts => ts.TotalMilliseconds)
            );
            
            if (avgLatency > TimeSpan.FromSeconds(5))
                return ConnectionHealth.Degraded;
        }
        
        return ConnectionHealth.Healthy;
    }
    
    public void RecordTimeout()
    {
        consecutiveTimeouts++;
    }
    
    public void RecordSuccess(TimeSpan latency)
    {
        consecutiveTimeouts = 0;
        consecutiveKeepAliveFailures = 0;
        
        latencySamples.Enqueue(latency);
        if (latencySamples.Count > 10)
            latencySamples.Dequeue();
    }
}

public enum ConnectionHealth
{
    Healthy,
    Degraded,
    Critical
}
```

---

## 4. Estrategia de Reconexión

### Flujo Óptimo

```
1. DETECTAR desconexión
   ↓
2. PAUSAR operaciones en curso
   ↓
3. LIMPIAR recursos (dispose sockets, timers)
   ↓
4. ESPERAR backoff delay
   ↓
5. VERIFICAR condiciones (no ban, no rate limit)
   ↓
6. INTENTAR reconexión
   ↓
7a. ÉXITO → Reanudar operaciones
7b. FALLO → Volver a paso 4 (incrementar backoff)
```

### Protecciones Críticas

**1. Prevenir Reconexiones Simultáneas**
```csharp
private readonly SemaphoreSlim reconnectSemaphore = new SemaphoreSlim(1, 1);

private async Task Reconnect()
{
    if (!await reconnectSemaphore.WaitAsync(0))
    {
        Log("Reconexión ya en curso, ignorando");
        return;
    }
    
    try
    {
        // Lógica de reconexión
    }
    finally
    {
        reconnectSemaphore.Release();
    }
}
```

**2. Respetar Ventanas de Cooldown**
```csharp
private DateTime nextAllowedReconnect = DateTime.MinValue;

private async Task Reconnect()
{
    var now = DateTime.UtcNow;
    if (now < nextAllowedReconnect)
    {
        var wait = nextAllowedReconnect - now;
        Log($"Reconexión aplazada {wait.TotalSeconds:F0}s");
        return;
    }
    
    // Proceder con reconexión
}
```

**3. Límite de Intentos**
```csharp
private const int MAX_RECONNECT_ATTEMPTS = 10;
private int reconnectAttempts = 0;

private async Task Reconnect()
{
    if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
    {
        Log("Máximo de intentos alcanzado, deshabilitando auto-reconexión");
        autoReconnect = false;
        return;
    }
    
    reconnectAttempts++;
    // Proceder con reconexión
}
```

---

## 5. Manejo de Errores Específicos

### TimeoutException
- **Causa**: Red lenta, servidor sobrecargado
- **Acción**: Backoff rápido (5-60s)
- **Reintentos**: Ilimitados

### SocketException
- **Causa**: Fallo de red, firewall, NAT
- **Acción**: Backoff rápido (5-60s)
- **Reintentos**: Ilimitados

### AuthenticationException
- **Causa**: Credenciales inválidas, cuenta baneada
- **Acción**: Backoff lento (30s-10min)
- **Reintentos**: Máximo 5, luego deshabilitar

### "Too many login attempts"
- **Causa**: Rate limiting del servidor
- **Acción**: Backoff agresivo (5-60 min)
- **Reintentos**: Ilimitados con delays largos

---

## 6. Optimizaciones Adicionales

### Connection Pooling
```csharp
// Mantener 2-3 conexiones en pool para failover rápido
private List<SoulseekClient> clientPool = new List<SoulseekClient>(3);
private int currentClientIndex = 0;

private SoulseekClient GetHealthyClient()
{
    for (int i = 0; i < clientPool.Count; i++)
    {
        var client = clientPool[currentClientIndex];
        currentClientIndex = (currentClientIndex + 1) % clientPool.Count;
        
        if (client.State.HasFlag(SoulseekClientStates.Connected))
            return client;
    }
    
    return null; // Todos desconectados
}
```

### Circuit Breaker Pattern
```csharp
public class CircuitBreaker
{
    private int failureCount = 0;
    private DateTime circuitOpenedAt = DateTime.MinValue;
    private CircuitState state = CircuitState.Closed;
    
    public bool AllowRequest()
    {
        if (state == CircuitState.Closed)
            return true;
        
        if (state == CircuitState.Open)
        {
            // Intentar cerrar después de timeout
            if (DateTime.UtcNow - circuitOpenedAt > TimeSpan.FromMinutes(5))
            {
                state = CircuitState.HalfOpen;
                return true;
            }
            return false;
        }
        
        // HalfOpen: permitir 1 intento
        return true;
    }
    
    public void RecordSuccess()
    {
        failureCount = 0;
        state = CircuitState.Closed;
    }
    
    public void RecordFailure()
    {
        failureCount++;
        
        if (failureCount >= 5)
        {
            state = CircuitState.Open;
            circuitOpenedAt = DateTime.UtcNow;
        }
    }
}

public enum CircuitState
{
    Closed,   // Normal
    Open,     // Bloqueado
    HalfOpen  // Probando
}
```

---

## 7. Configuración Recomendada Final

### Para Uso Normal (24/7)
```csharp
// TCP Keep-Alive
keepaliveTime = 60s
keepaliveInterval = 10s

// Reconexión
autoReconnect = true
maxReconnectAttempts = unlimited
baseBackoff = 5s
maxBackoff = 60s
jitterPercent = 25%

// Health Check
healthCheckInterval = 30s
timeoutThreshold = 3
```

### Para Uso Intensivo (Stress Test)
```csharp
// TCP Keep-Alive (más agresivo)
keepaliveTime = 30s
keepaliveInterval = 5s

// Reconexión (más rápida)
autoReconnect = true
maxReconnectAttempts = unlimited
baseBackoff = 2s
maxBackoff = 30s
jitterPercent = 25%

// Health Check (más frecuente)
healthCheckInterval = 15s
timeoutThreshold = 2
```

### Para Evitar Bans
```csharp
// TCP Keep-Alive (conservador)
keepaliveTime = 120s
keepaliveInterval = 30s

// Reconexión (muy conservadora)
autoReconnect = true
maxReconnectAttempts = 5
baseBackoff = 30s
maxBackoff = 600s (10 min)
jitterPercent = 50%

// Rate Limiting
maxSearchesPerMinute = 10
maxDownloadsParallel = 3
respectServerCooldowns = true
```

---

## 8. Métricas de Monitoreo

### KPIs Críticos
1. **Uptime**: % tiempo conectado vs. desconectado
2. **MTBF** (Mean Time Between Failures): Tiempo promedio entre desconexiones
3. **MTTR** (Mean Time To Reconnect): Tiempo promedio para reconectar
4. **Reconnect Success Rate**: % reconexiones exitosas
5. **Ban Rate**: Frecuencia de bans/rate limits

### Logging Recomendado
```csharp
// Cada desconexión
Log($"DISCONNECT: Reason={reason}, Uptime={uptime}, Operations={opsCount}");

// Cada reconexión
Log($"RECONNECT: Attempt={attempt}, Delay={delay}, Success={success}");

// Cada hora
Log($"STATS: Uptime={uptime}%, MTBF={mtbf}, MTTR={mttr}, Bans={bans}");
```

---

## 9. Comparación con Implementaciones de Referencia

### Nicotine+ (Python)
- ✅ Usa TCP keepalive nativo
- ✅ Reconexión automática con backoff
- ✅ Manejo de rate limiting
- ❌ No usa jitter (puede causar thundering herd)
- ❌ Backoff no es exponencial (lineal)

### SoulseekQt (C++)
- ✅ TCP keepalive configurado
- ❌ Reconexión manual por defecto
- ❌ No hay backoff inteligente
- ❌ No detecta bans automáticamente

### SlskDown (Esta implementación)
- ✅ TCP keepalive con valores optimizados
- ✅ Exponential backoff con jitter
- ✅ Detección automática de bans
- ✅ Circuit breaker para protección
- ✅ Connection pooling para failover
- ✅ Health monitoring continuo

---

## 10. Recomendaciones Finales

### DO ✅
1. **Implementar TCP keepalive** con valores agresivos (60s/10s)
2. **Usar exponential backoff con jitter** para evitar thundering herd
3. **Detectar y respetar rate limits** del servidor
4. **Monitorear salud de conexión** continuamente
5. **Pausar operaciones** durante reconexión
6. **Limpiar recursos** antes de reconectar
7. **Logging exhaustivo** para debugging

### DON'T ❌
1. **No reconectar inmediatamente** sin backoff
2. **No ignorar errores de autenticación** (pueden causar bans)
3. **No hacer operaciones** durante reconexión
4. **No usar delays fijos** (usar exponential backoff)
5. **No reconectar infinitamente** sin límites
6. **No ignorar señales del servidor** (rate limits, bans)
7. **No usar ServerPing** (obsoleto, usar TCP keepalive)

---

## Referencias

1. [Nicotine+ Protocol Documentation](https://nicotine-plus.org/doc/SLSKPROTOCOL.html)
2. [TCP Keepalive HOWTO](http://tldp.org/HOWTO/TCP-Keepalive-HOWTO/)
3. [Exponential Backoff - Wikipedia](https://en.wikipedia.org/wiki/Exponential_backoff)
4. [Microsoft: Implement Retries with Exponential Backoff](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-retries-exponential-backoff)
5. [C# Setting Socket Keep-Alive](https://www.darchuk.net/2019/01/04/c-setting-socket-keep-alive/)
6. [Nicotine+ GitHub Issues - Auto Reconnect](https://github.com/nicotine-plus/nicotine-plus/issues/2168)

---

**Última actualización**: 2024-11-23
**Versión**: 1.0
**Autor**: Cascade AI (basado en investigación exhaustiva)
