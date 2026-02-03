# 🚀 CONFIGURACIÓN ÓPTIMA PARA DESCARGAS RÁPIDAS

## ⚡ MODO TURBO (Recomendado para máxima velocidad)

### Activar en: Pestaña "Configuración"

**✅ Modo Turbo**: Activar checkbox "⚡ Modo Turbo"
- **Descargas simultáneas**: 8 (automático al activar Turbo)
- **Búsquedas paralelas**: 12 (automático)
- **Timeout**: 20 segundos (automático)

**Resultado**: 
- 8 archivos descargándose al mismo tiempo
- Búsquedas más rápidas con 12 threads paralelos
- Menor timeout para respuestas más ágiles

---

## 🔥 MODO AGRESIVO (Máxima velocidad por 30 minutos)

### Activar en: Pestaña "Configuración" → Botón "🔥 Modo Agresivo"

**Configuración temporal (30 minutos)**:
- **Descargas simultáneas**: 15
- **Búsquedas paralelas**: 20
- **Timeout**: 15 segundos

**⚠️ Advertencia**: 
- Consume más recursos del sistema
- Puede saturar la conexión de red
- Se desactiva automáticamente después de 30 minutos
- Ideal para descargar muchos archivos rápidamente

---

## 📊 PRIORIDAD AUTOMÁTICA POR TAMAÑO

### Activar en: Pestaña "Configuración"

**✅ Prioridad automática por tamaño**: Activar checkbox

**Comportamiento**:
- Archivos pequeños se descargan primero
- Completa más descargas rápidamente
- Mejor sensación de progreso
- Ideal para colecciones con muchos archivos pequeños

---

## 🎯 ESTRATEGIA DE COLA

### Configuración actual: `QueuePrioritizationStrategy.Balanced`

**Estrategias disponibles** (requiere modificar código):

1. **Balanced** (ACTUAL - Recomendado):
   - Balance entre velocidad y tamaño
   - Prioriza archivos pequeños para completar más descargas

2. **FastestFirst**:
   - Descarga primero de los usuarios más rápidos
   - Maximiza throughput total

3. **SmallestFirst**:
   - Archivos más pequeños primero
   - Completa más descargas rápidamente

4. **LargestFirst**:
   - Archivos grandes primero
   - Útil si necesitas archivos grandes urgentemente

5. **RarestFirst**:
   - Archivos con menos fuentes primero
   - Asegura obtener archivos difíciles de encontrar

---

## 🔄 REINTENTOS Y ALTERNATIVAS

### Configuración actual en UI:

**🔄 Reintentos automáticos**: 3 (configurable 0-10)
- Número de veces que reintenta con el mismo proveedor

**🔍 Proveedores alternativos**: 3 (configurable 0-10)
- Número de proveedores diferentes a intentar si el primero falla

**Límite global**: 15 intentos totales máximo (constante MAX_TOTAL_ATTEMPTS)

**Recomendación para velocidad**:
- Reintentos: 2 (menos tiempo esperando)
- Proveedores alternativos: 5 (más opciones)

---

## ⚙️ OTRAS CONFIGURACIONES QUE AFECTAN VELOCIDAD

### En pestaña "Configuración":

1. **🤖 Modo Automático**: 
   - Activar para limpieza y optimización automática
   - Mantiene la cola limpia

2. **📁 Organizar por autor**:
   - No afecta velocidad de descarga
   - Solo organiza archivos en carpetas

3. **Búsquedas simultáneas**: 
   - Turbo: 12 (óptimo)
   - Normal: 3
   - Agresivo: 20

4. **Timeout de búsqueda**:
   - Turbo: 20 segundos
   - Normal: 30 segundos
   - Agresivo: 15 segundos
   - **Menor = más rápido pero puede perder resultados**

---

## 📈 CONFIGURACIÓN RECOMENDADA SEGÚN ESCENARIO

### 🎯 Uso Normal Diario
```
✅ Modo Turbo: ACTIVADO
✅ Descargas simultáneas: 8
✅ Prioridad por tamaño: ACTIVADO
✅ Reintentos: 3
✅ Proveedores alternativos: 3
```

### 🚀 Descarga Masiva (muchos archivos)
```
✅ Modo Agresivo: ACTIVADO (30 min)
✅ Descargas simultáneas: 15
✅ Prioridad por tamaño: ACTIVADO
✅ Reintentos: 2
✅ Proveedores alternativos: 5
```

### 🐢 Conexión Lenta o PC con pocos recursos
```
❌ Modo Turbo: DESACTIVADO
✅ Descargas simultáneas: 3
✅ Prioridad por tamaño: ACTIVADO
✅ Reintentos: 3
✅ Proveedores alternativos: 2
```

### 📚 Archivos Raros/Difíciles
```
✅ Modo Turbo: ACTIVADO
✅ Descargas simultáneas: 5
❌ Prioridad por tamaño: DESACTIVADO
✅ Reintentos: 5
✅ Proveedores alternativos: 8
```

---

## 🔍 VERIFICAR ESTADO DEL GESTOR

### En pestaña "Vaciar" → Botón "📊 Estado"

Muestra:
- ✅ Gestor activo: SÍ/NO
- 🔢 Límite simultáneas actual
- 🎯 Slots disponibles
- 📊 Estadísticas de descargas

**Si el gestor está inactivo**: Se reinicia automáticamente

---

## 💡 TIPS ADICIONALES

1. **Conexión estable**: Asegúrate de estar conectado a Soulseek (pestaña principal)

2. **Blacklist**: Limpia usuarios problemáticos periódicamente para no desperdiciar reintentos

3. **Modo Automático**: Actívalo para que limpie descargas completadas automáticamente

4. **Monitoreo**: Revisa la pestaña "Descargas" para ver progreso en tiempo real

5. **Reinicio del gestor**: Si las descargas se detienen, usa el botón "📊 Estado" en tab Vaciar

---

## 🎮 CONFIGURACIÓN ACTUAL DEL SISTEMA

**Valores por defecto**:
- maxSimultaneousDownloads: 3
- maxParallelSearches: 3
- maxRetries: 3
- maxAlternativeRetries: 3
- searchTimeout: 30 segundos
- queueStrategy: Balanced
- priorityBySize: false (desactivado)

**Con Modo Turbo**:
- maxSimultaneousDownloads: 8
- maxParallelSearches: 12
- searchTimeout: 20 segundos

**Con Modo Agresivo**:
- maxSimultaneousDownloads: 15
- maxParallelSearches: 20
- searchTimeout: 15 segundos

---

## ⚡ RESUMEN RÁPIDO

**Para máxima velocidad AHORA**:
1. Ve a pestaña "Configuración"
2. Activa "⚡ Modo Turbo"
3. Activa "📊 Prioridad automática por tamaño"
4. Ajusta "🔍 Proveedores alternativos" a 5
5. ¡Listo! Las descargas irán 2-3x más rápido

**Para velocidad extrema temporal**:
1. Haz clic en "🔥 Modo Agresivo" (30 minutos)
2. Las descargas irán 4-5x más rápido
3. Se desactiva automáticamente

---

## 📝 NOTAS TÉCNICAS

- El sistema usa un **SemaphoreSlim** para controlar descargas simultáneas
- **Backoff exponencial** en reintentos: 30s, 60s, 120s, 240s, 480s
- **Circuit breaker** para proveedores problemáticos (blacklist temporal)
- **Caché de proveedores** para evitar búsquedas repetidas
- **Auto-limpieza** de descargas fallidas después de 7 días
- **Detección de descargas atascadas** (>60s en 0%)

---

Generado automáticamente por SlskDown
Última actualización: 2025-11-18
