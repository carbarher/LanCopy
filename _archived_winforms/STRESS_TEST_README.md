# 🧪 Herramienta de Prueba de Carga - Cliente Soulseek

Esta herramienta permite realizar pruebas de carga y rendimiento del cliente Soulseek para evaluar:
- Número de búsquedas concurrentes que puede manejar
- Tiempo de respuesta de las búsquedas
- Estabilidad de la conexión durante períodos prolongados
- Cantidad de resultados obtenidos

## 📋 Requisitos

- .NET 8.0 SDK
- Cuenta válida de Soulseek
- Conexión a Internet estable

## 🚀 Ejecución

### Opción 1: Usar el script batch
```batch
run_stress_test.bat
```

### Opción 2: Ejecutar directamente con dotnet
```batch
dotnet run --project StressTestRunner.csproj
```

### Opción 3: Pasar credenciales como argumentos
```batch
dotnet run --project StressTestRunner.csproj -- usuario contraseña
```

## 📊 Tipos de Prueba

### 1. Prueba Rápida (30 segundos)
- **Búsquedas concurrentes:** 5
- **Duración:** 30 segundos
- **Ideal para:** Verificación rápida del funcionamiento

### 2. Prueba Moderada (60 segundos)
- **Búsquedas concurrentes:** 10
- **Duración:** 60 segundos
- **Ideal para:** Pruebas de rendimiento estándar

### 3. Prueba Intensiva (120 segundos)
- **Búsquedas concurrentes:** 20
- **Duración:** 120 segundos
- **Ideal para:** Evaluar límites de rendimiento

### 4. Prueba Extrema (180 segundos)
- **Búsquedas concurrentes:** 30
- **Duración:** 180 segundos
- **Ideal para:** Pruebas de estrés máximo

### 5. Prueba Personalizada
- Permite configurar manualmente:
  - Número de búsquedas concurrentes
  - Duración en segundos

## 📈 Métricas Reportadas

La herramienta proporciona las siguientes métricas:

### Estadísticas Generales
- **Tiempo total:** Duración real de la prueba
- **Búsquedas exitosas:** Número de búsquedas completadas con éxito
- **Búsquedas fallidas:** Número de búsquedas que fallaron
- **Total de resultados:** Cantidad total de archivos encontrados
- **Tasa de éxito:** Porcentaje de búsquedas exitosas
- **Búsquedas por segundo:** Throughput de búsquedas
- **Resultados por búsqueda:** Promedio de archivos por búsqueda

### Tiempos de Respuesta
- **Mínimo:** Tiempo de respuesta más rápido
- **Promedio:** Tiempo de respuesta medio
- **Mediana (P50):** 50% de las búsquedas fueron más rápidas
- **P95:** 95% de las búsquedas fueron más rápidas
- **P99:** 99% de las búsquedas fueron más rápidas
- **Máximo:** Tiempo de respuesta más lento

## 🔍 Cómo Funciona

1. **Conexión:** Se establece una conexión con el servidor Soulseek
2. **Búsquedas Concurrentes:** Se lanzan múltiples búsquedas en paralelo
3. **Queries Variadas:** Utiliza 15 términos de búsqueda diferentes:
   - rock music, jazz, classical, electronic, pop
   - blues, metal, indie, hip hop, ambient
   - techno, house, trance, dubstep, reggae
4. **Monitoreo:** Actualiza estadísticas cada 5 segundos
5. **Reporte:** Al finalizar, muestra un resumen completo

## ⚠️ Consideraciones

- **Uso responsable:** No abuse del servidor Soulseek con pruebas excesivas
- **Límites de la red:** Los resultados dependen de tu conexión a Internet
- **Carga del servidor:** La red Soulseek puede estar más o menos ocupada
- **Timeout de conexión:** 30 segundos con 3 reintentos automáticos
- **Timeout de búsqueda:** Cada búsqueda tiene un timeout de 10 segundos
- **Límites de concurrencia:** Máximo 10 búsquedas simultáneas
- **Límites por búsqueda:** 
  - Máximo 50 respuestas por búsqueda
  - Máximo 100 archivos por búsqueda

## 📝 Ejemplo de Salida

```
=== PRUEBA DE CARGA DEL CLIENTE SOULSEEK ===
Usuario: carbar
Búsquedas concurrentes: 10
Duración: 60 segundos
Iniciando en 3 segundos...

Conectando a Soulseek...
✓ Conectado exitosamente

[5s] Búsquedas exitosas: 12 | Fallidas: 0 | Resultados: 1847 | Restante: 55s
[10s] Búsquedas exitosas: 24 | Fallidas: 1 | Resultados: 3692 | Restante: 50s
...

✓ Desconectado del servidor

=== RESULTADOS DE LA PRUEBA ===
Tiempo total: 60.23 segundos
Búsquedas exitosas: 142
Búsquedas fallidas: 3
Total de búsquedas: 145
Total de resultados obtenidos: 21847
Tasa de éxito: 97.93%
Búsquedas por segundo: 2.41
Resultados por búsqueda: 153.84

=== TIEMPOS DE BÚSQUEDA (ms) ===
Mínimo: 2341 ms
Promedio: 8234.56 ms
Mediana (P50): 7892 ms
P95: 9876 ms
P99: 9987 ms
Máximo: 10234 ms

=== PRUEBA COMPLETADA ===
```

## 🛠️ Solución de Problemas

### Error de conexión (timeout)
**Síntoma:** `The wait timed out after 30000 milliseconds`

**Soluciones:**
- La herramienta reintenta automáticamente 3 veces
- Verifica tus credenciales en `config.json`
- Asegúrate de tener conexión a Internet estable
- Comprueba que el servidor Soulseek esté disponible
- Si falla consistentemente, prueba más tarde (servidor saturado)
- Considera usar VPN si tu ISP bloquea P2P

**Ver:** `SOLUCION_TIMEOUT_CONEXION.md` para diagnóstico detallado

### Muchas búsquedas fallidas
- Reduce el número de búsquedas concurrentes
- Verifica la estabilidad de tu conexión
- Intenta en otro momento (el servidor puede estar saturado)

### Tiempos de respuesta muy altos
- Normal en redes congestionadas
- Considera reducir el número de búsquedas concurrentes
- Verifica tu velocidad de Internet

## 🔧 Características

- **Múltiples niveles de prueba:** Rápida, Moderada, Intensiva, Extrema y Personalizada
- **Búsquedas concurrentes:** Simula múltiples usuarios buscando simultáneamente
- **Reconexión automática:** Se reconecta automáticamente si se pierde la conexión
- **Límite de concurrencia:** Máximo 10 búsquedas simultáneas para no saturar
- **Monitoreo en tiempo real:** Muestra progreso cada 5 segundos con estado de conexión
- **Estadísticas detalladas:** Tiempos de respuesta, throughput, tasas de éxito, reconexiones
- **Análisis de percentiles:** P50, P95, P99 para identificar outliers

## 📧 Soporte

Para reportar problemas o sugerencias, contacta al desarrollador.
