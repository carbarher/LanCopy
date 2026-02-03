# Análisis del Sistema de Descargas - SlskDown

## Problema Actual
Las descargas siguen fallando o teniendo problemas. Necesitamos identificar y solucionar los puntos débiles.

## Arquitectura Actual

### 1. Sistema de Reintentos
- **RetryPolicy.cs**: Implementa reintentos con backoff exponencial
  - MaxAttempts: 3 por defecto
  - InitialDelay: 2000ms
  - BackoffMultiplier: 2.0
  - Circuit Breaker: 3 fallos → bloqueo de 5 minutos

### 2. Flujo de Descarga (DownloadFileAsync)
```
1. Verificar circuit breaker del usuario
2. Si está abierto → omitir descarga
3. Ejecutar descarga con RetryPolicy.ExecuteWithRetry
   - 3 intentos máximo
   - Delay inicial: 2000ms
   - Backoff exponencial
4. Si falla → TrackFileFailure
5. Si está blacklisteado → no reintentar
6. Si maxRetries > 0 → agregar a retryQueue
```

### 3. Excepciones Retriables
- SocketException
- WebException
- TimeoutException
- IOException
- Mensajes con: "connection", "network", "timeout", "timed out"

## Problemas Identificados

### 1. Timeouts Agresivos
- El timeout por defecto puede ser muy corto
- No hay timeout configurable por archivo
- No se ajusta según el tamaño del archivo

### 2. Circuit Breaker Demasiado Estricto
- 3 fallos → bloqueo de 5 minutos
- No distingue entre tipos de error
- Puede bloquear usuarios buenos por problemas temporales

### 3. Falta de Logging Detallado
- No se registra el tipo específico de error
- No se registra el progreso antes del fallo
- No se registra información de red (velocidad, latencia)

### 4. No Hay Gestión de Prioridades Dinámicas
- Archivos pequeños deberían tener prioridad
- Archivos que fallan repetidamente deberían bajar de prioridad
- No hay sistema de "fast lane" para archivos críticos

### 5. Falta de Validación Post-Descarga
- No se verifica el tamaño del archivo descargado
- No se verifica la integridad (checksum)
- Archivos corruptos pueden marcarse como "completados"

## Preguntas para Ollama

1. **Timeouts**: ¿Cuál es la mejor estrategia para calcular timeouts dinámicos basados en el tamaño del archivo?

2. **Circuit Breaker**: ¿Cómo mejorar el circuit breaker para ser más inteligente?
   - ¿Debería distinguir entre tipos de error?
   - ¿Debería tener diferentes umbrales según el tipo de fallo?
   - ¿Debería resetear gradualmente en lugar de todo o nada?

3. **Reintentos**: ¿Es 3 intentos suficiente? ¿Debería ser adaptativo según:
   - Tamaño del archivo
   - Historial de fallos del usuario
   - Tipo de error

4. **Validación**: ¿Qué validaciones post-descarga son esenciales?
   - Verificación de tamaño
   - Checksums
   - Detección de archivos corruptos

5. **Priorización**: ¿Cómo implementar un sistema de prioridades dinámico efectivo?

6. **Logging**: ¿Qué métricas adicionales deberíamos registrar para diagnosticar problemas?

7. **Concurrencia**: Actualmente se permiten descargas paralelas. ¿Hay riesgos? ¿Límites recomendados?

8. **Recuperación**: Si una descarga falla al 90%, ¿deberíamos implementar resume/partial download?
