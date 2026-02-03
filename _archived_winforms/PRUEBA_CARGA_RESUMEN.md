# 📊 Resumen: Sistema de Prueba de Carga para Cliente Soulseek

## 🎯 Objetivo

Evaluar el rendimiento y la estabilidad del cliente Soulseek bajo diferentes cargas de trabajo, midiendo:
- Capacidad de búsquedas concurrentes
- Tiempos de respuesta
- Estabilidad de conexión
- Throughput de resultados

## 📁 Archivos Creados

### 1. `StressTest.cs`
**Propósito:** Clase principal que implementa la lógica de prueba de carga

**Funcionalidades:**
- Conexión al servidor Soulseek
- Ejecución de búsquedas concurrentes
- Recolección de métricas en tiempo real
- Generación de reportes estadísticos

**Métodos principales:**
- `RunStressTest()` - Ejecuta prueba personalizada
- `QuickTest()` - 5 búsquedas, 30 segundos
- `ModerateTest()` - 10 búsquedas, 60 segundos
- `IntensiveTest()` - 20 búsquedas, 120 segundos
- `ExtremeTest()` - 30 búsquedas, 180 segundos

### 2. `TestRunner.cs`
**Propósito:** Interfaz de línea de comandos para ejecutar las pruebas

**Características:**
- Menú interactivo
- Entrada segura de contraseña (oculta caracteres)
- Soporte para argumentos de línea de comandos
- Manejo robusto de errores

### 3. `StressTestRunner.csproj`
**Propósito:** Archivo de proyecto .NET independiente

**Configuración:**
- Target Framework: .NET 8.0
- Tipo: Aplicación de consola
- Dependencia: Soulseek 1.0.0-rc2.2

### 4. Scripts de Ejecución

#### `run_stress_test.bat`
Script interactivo que solicita credenciales y permite elegir tipo de prueba

#### `quick_test.bat`
Ejecuta automáticamente una prueba rápida con credenciales predefinidas

### 5. Documentación

#### `STRESS_TEST_README.md`
Manual completo de usuario con:
- Instrucciones de uso
- Descripción de tipos de prueba
- Explicación de métricas
- Ejemplos de salida
- Solución de problemas

## 🔬 Tipos de Prueba Disponibles

| Tipo | Búsquedas | Duración | Uso Recomendado |
|------|-----------|----------|-----------------|
| Rápida | 5 | 30s | Verificación funcional |
| Moderada | 10 | 60s | Prueba estándar |
| Intensiva | 20 | 120s | Evaluación de límites |
| Extrema | 30 | 180s | Prueba de estrés |
| Personalizada | Variable | Variable | Casos específicos |

## 📈 Métricas Recopiladas

### Métricas de Rendimiento
- **Búsquedas exitosas/fallidas:** Conteo de operaciones
- **Tasa de éxito:** Porcentaje de búsquedas completadas
- **Throughput:** Búsquedas por segundo
- **Resultados totales:** Archivos encontrados
- **Promedio de resultados:** Por búsqueda

### Métricas de Latencia
- **Mínimo:** Mejor tiempo de respuesta
- **Promedio:** Tiempo medio
- **Mediana (P50):** Percentil 50
- **P95:** Percentil 95 (SLA típico)
- **P99:** Percentil 99 (casos extremos)
- **Máximo:** Peor tiempo de respuesta

## 🎲 Estrategia de Prueba

### Queries de Búsqueda
Se utilizan 15 términos variados para simular uso real:
```
rock music, jazz, classical, electronic, pop,
blues, metal, indie, hip hop, ambient,
techno, house, trance, dubstep, reggae
```

### Patrón de Ejecución
1. **Inicio:** Conexión al servidor Soulseek
2. **Ejecución:** Búsquedas concurrentes con queries aleatorias
3. **Pausa:** 2-5 segundos entre búsquedas (aleatorio)
4. **Monitoreo:** Actualización cada 5 segundos
5. **Finalización:** Desconexión y reporte

### Configuración de Búsqueda
- **Timeout:** 10 segundos por búsqueda
- **Límite de respuestas:** 50 por búsqueda
- **Límite de archivos:** 100 por búsqueda

## 🚀 Cómo Ejecutar

### Método 1: Script Interactivo
```batch
run_stress_test.bat
```
1. Ingresa usuario (o Enter para 'carbar')
2. Ingresa contraseña (oculta)
3. Selecciona tipo de prueba (1-5)
4. Observa resultados en tiempo real

### Método 2: Prueba Rápida Automática
```batch
quick_test.bat
```
Ejecuta automáticamente prueba rápida con credenciales del config.json

### Método 3: Línea de Comandos
```batch
dotnet run --project StressTestRunner.csproj -- usuario contraseña
```
Luego selecciona tipo de prueba

### Método 4: Prueba Personalizada
```batch
dotnet run --project StressTestRunner.csproj
```
1. Ingresa credenciales
2. Selecciona opción 5 (Personalizada)
3. Define número de búsquedas y duración

## 📊 Ejemplo de Resultados

```
=== PRUEBA DE CARGA DEL CLIENTE SOULSEEK ===
Usuario: carbar
Búsquedas concurrentes: 10
Duración: 60 segundos

Conectando a Soulseek...
✓ Conectado exitosamente

[5s] Búsquedas exitosas: 12 | Fallidas: 0 | Resultados: 1847 | Restante: 55s
[10s] Búsquedas exitosas: 24 | Fallidas: 1 | Resultados: 3692 | Restante: 50s

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
```

## 🎯 Casos de Uso

### 1. Verificación de Funcionalidad
**Prueba:** Rápida (30s)
**Objetivo:** Confirmar que el cliente funciona correctamente

### 2. Benchmark de Rendimiento
**Prueba:** Moderada (60s)
**Objetivo:** Establecer métricas base de rendimiento

### 3. Prueba de Límites
**Prueba:** Intensiva (120s)
**Objetivo:** Determinar capacidad máxima sostenible

### 4. Prueba de Estabilidad
**Prueba:** Extrema (180s)
**Objetivo:** Verificar estabilidad bajo carga prolongada

### 5. Análisis de Regresión
**Prueba:** Personalizada
**Objetivo:** Comparar rendimiento entre versiones

## ⚠️ Consideraciones Importantes

### Limitaciones de Red
- Los resultados dependen de la conexión a Internet
- La carga del servidor Soulseek afecta los tiempos
- Hora del día puede influir en disponibilidad de peers

### Uso Responsable
- No ejecutar pruebas extremas frecuentemente
- Respetar los límites del servidor Soulseek
- Considerar el impacto en otros usuarios

### Interpretación de Resultados
- **Tasa de éxito > 95%:** Excelente
- **Tasa de éxito 90-95%:** Bueno
- **Tasa de éxito < 90%:** Revisar configuración/red
- **P95 < 10s:** Rendimiento óptimo
- **P95 > 15s:** Posible congestión

## 🔧 Personalización

Para modificar el comportamiento de las pruebas, edita `StressTest.cs`:

### Cambiar queries de búsqueda
```csharp
var searchQueries = new[] 
{ 
    "tu", "lista", "de", "queries"
};
```

### Ajustar timeouts
```csharp
var searchOptions = new SearchOptions
{
    SearchTimeout = 15000,  // 15 segundos
    ResponseLimit = 100,    // 100 respuestas
    FileLimit = 200         // 200 archivos
};
```

### Modificar pausas
```csharp
await Task.Delay(random.Next(1000, 3000), ct); // 1-3 segundos
```

## 📝 Próximas Mejoras Sugeridas

1. **Exportación de resultados:** Guardar métricas en CSV/JSON
2. **Gráficos:** Visualización de tiempos de respuesta
3. **Comparación:** Comparar resultados entre ejecuciones
4. **Pruebas de descarga:** Evaluar rendimiento de descargas
5. **Monitoreo de recursos:** CPU, memoria, red
6. **Logs detallados:** Registro de cada búsqueda

## 🏆 Conclusión

Este sistema de prueba de carga proporciona una herramienta completa para:
- ✅ Evaluar rendimiento del cliente Soulseek
- ✅ Identificar cuellos de botella
- ✅ Validar estabilidad bajo carga
- ✅ Establecer benchmarks de referencia
- ✅ Detectar regresiones de rendimiento

**Resultado esperado:** Mejor comprensión del comportamiento del cliente bajo diferentes condiciones de carga.
