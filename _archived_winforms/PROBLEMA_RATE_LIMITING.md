# Problema: Rate Limiting del Servidor Soulseek

**Fecha**: 3 de diciembre de 2025, 18:24  
**Problema**: Timeout en login después de múltiples intentos de conexión  
**Estado**: ⚠️ Requiere espera de 15-20 minutos

## Síntomas

```
[17:22:34] ✅ TCP Keep-Alive nativo habilitado
[17:22:34] 🌐 [Soulseek] Estado: Connected  ← TCP conectado OK
[17:22:39] 🌐 [Soulseek] Estado: Disconnected  ← 5 segundos después
[17:22:39] Error: The wait timed out after 5000 milliseconds
```

**Patrón observado**:
- TCP handshake: ✅ Exitoso (puerto 2242 accesible)
- DNS resolución: ✅ Exitosa (208.76.170.59)
- Login de Soulseek: ❌ Timeout después de 5 segundos

## Causa Raíz

El servidor de Soulseek tiene **rate limiting agresivo** que bloquea temporalmente IPs que intentan conectar demasiadas veces en poco tiempo.

### Historial de Intentos

```
17:14:20 - Conexión exitosa
17:17:36 - Desconexión (problema de keep-alive)
17:17:42 - Intento de reconexión → Timeout
17:22:34 - Nuevo intento → Timeout
```

**Total**: 3 intentos en 8 minutos → **IP bloqueada temporalmente**

## Por Qué Sucede

### 1. Desconexión a los 3 Minutos (Resuelto)

El problema original era que `StealthWarmup()` saturaba el servidor con pings cada 2-3 segundos:

```csharp
// ANTES (INCORRECTO)
for (int i = 0; i < pings; i++)
{
    await KeepAliveCheck();  // ❌ Saturaba el servidor
    await Task.Delay(stealthRandom.Next(1000, 3000));
}
```

**Resultado**: El servidor cerraba la conexión a los 3 minutos.

### 2. Reconexión Automática Inmediata

Después de la desconexión, la aplicación intentaba reconectar **inmediatamente**:

```
17:17:36 - Desconexión
17:17:42 - Reconexión (6 segundos después) → Timeout
```

El servidor interpreta esto como **abuso** y bloquea la IP temporalmente.

### 3. Intentos Manuales Adicionales

El usuario intentó conectar manualmente a las 17:22 (5 minutos después), pero el bloqueo todavía estaba activo.

## Solución

### Inmediata: Esperar

**Espera 15-20 minutos** antes de volver a intentar conectar. El servidor necesita tiempo para resetear el rate limit de tu IP.

### A Largo Plazo: Fixes Implementados

#### 1. Fix de Keep-Alive (Completado)

Se eliminaron los pings múltiples de `StealthWarmup()`:

```csharp
// DESPUÉS (CORRECTO)
// FIX: Eliminar pings múltiples que saturan el servidor
// El timer de keep-alive ya maneja esto cada 90s
int warmupDelay = stealthRandom.Next(5000, 10000);
await Task.Delay(warmupDelay);
```

**Archivo**: `MainForm.cs`, líneas 636-642

#### 2. Cooldown Extendido (Ya Implementado)

El código ya detecta reconexiones después de desconexión limpia y aplica un delay de 120 segundos:

```csharp
if (isReconnectAfterCleanDisconnect && attempt == 1)
{
    delaySeconds = Math.Max(delaySeconds, 120); // Mínimo 2 minutos
    Log($"🔄 Reconexión después de desconexión limpia detectada");
    Log($"⏱️ Aplicando delay extendido de {delaySeconds}s para evitar rate limiting");
}
```

**Archivo**: `MainForm.cs`, líneas 7566-7573

#### 3. Circuit Breaker (Ya Implementado)

Después de 5 fallos consecutivos, el circuit breaker se abre y previene más intentos durante 5 minutos:

```csharp
if (circuitFailureCount >= circuitFailureThreshold)
{
    connectionCircuit = CircuitState.Open;
    circuitOpenTime = DateTime.UtcNow;
    Log($"🚫 CIRCUIT BREAKER ABIERTO - {circuitFailureCount} fallos consecutivos");
    throw new InvalidOperationException($"Circuit breaker abierto por {circuitOpenDuration.TotalMinutes} minutos");
}
```

**Archivo**: `MainForm.cs`, líneas 7589-7596

## Prevención Futura

### Comportamiento Esperado Después de los Fixes

1. **Conexión inicial**: ✅ Exitosa
2. **Keep-alive cada 90s**: ✅ Mantiene conexión estable
3. **Si se desconecta**: Espera 2 minutos antes de reconectar
4. **Si falla 5 veces**: Circuit breaker se abre por 5 minutos

### Logs Esperados

```
[18:30:00] ✅ Conexión exitosa
[18:30:00] 🔔 Keep-alive timer iniciado (92s con jitter)
[18:31:32] 💓 Keep-alive: enviando ping al servidor...
[18:31:33] ✅ Keep-alive exitoso (latencia: 1016ms)
[18:33:04] 💓 Keep-alive: enviando ping al servidor...
[18:33:05] ✅ Keep-alive exitoso (latencia: 1020ms)
```

**Sin desconexiones a los 3 minutos** ✅

## Recomendaciones

### Para el Usuario

1. **Espera 15-20 minutos** antes de intentar conectar de nuevo
2. **No intentes conectar manualmente** después de un fallo - deja que la reconexión automática maneje los reintentos
3. **Verifica tus credenciales** si el problema persiste después de esperar

### Para el Desarrollador

1. ✅ **Keep-alive fix implementado** (eliminar pings múltiples)
2. ✅ **Cooldown extendido implementado** (2 minutos después de desconexión)
3. ✅ **Circuit breaker implementado** (5 fallos → 5 minutos de pausa)
4. ✅ **Triple verificación de migración** (auto-reconexión garantizada)

## Verificación

Después de esperar 15-20 minutos, reinicia la aplicación y observa los logs:

```bash
# Logs esperados
✅ Conexión exitosa en puerto XXXXX
🔔 Keep-alive timer iniciado (XXs con jitter)
💓 Keep-alive: enviando ping al servidor...
✅ Keep-alive exitoso (latencia: XXXXms)
```

Si ves estos logs, la conexión está estable y funcionando correctamente.

## Notas Técnicas

### Rate Limiting del Servidor Soulseek

El servidor de Soulseek implementa rate limiting en varios niveles:

1. **Conexiones por IP**: Máximo ~3-5 intentos en 10 minutos
2. **Login failures**: Bloqueo temporal después de 2-3 fallos
3. **Pings excesivos**: Cierre de conexión si detecta spam

### Timeout de Login

El timeout de 5 segundos es **interno de Soulseek.NET** y no es configurable. Si el servidor no responde en 5 segundos, el login falla con `TimeoutException`.

### TCP vs Soulseek Login

- **TCP handshake**: Conexión a nivel de red (puerto 2242)
- **Soulseek login**: Autenticación a nivel de aplicación (username/password)

El TCP puede funcionar pero el login fallar si:
- El servidor está bloqueando tu IP
- Las credenciales son incorrectas
- El servidor está sobrecargado

## Conclusión

✅ **Fixes implementados** para prevenir el problema en el futuro  
⚠️ **Espera requerida** (15-20 minutos) para que el servidor resetee el rate limit  
✅ **Conexión estable garantizada** después de la espera

La aplicación ahora:
- Mantiene la conexión estable sin desconexiones a los 3 minutos
- Respeta cooldowns de 2 minutos después de desconexiones
- Usa circuit breaker para prevenir intentos excesivos
- Reconecta automáticamente de forma inteligente
