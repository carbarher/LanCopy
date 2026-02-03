# 🔧 AJUSTES DE ESTABILIDAD - EVITAR BANS TEMPORALES

## 🚨 Problema Detectado

Durante la búsqueda automática, Soulseek **banea temporalmente la IP**:
```
❌ ConnectAsync falló: TimeoutException - The wait timed out after 5000 milliseconds
❌ Error final de conexión
```

**Causa:** Rate limit y paralelismo excesivos causan que el servidor de Soulseek bloquee temporalmente la IP (5-10 minutos).

---

## ✅ SOLUCIÓN APLICADA

### **Cambios en `MainForm.cs` (líneas 3159-3162):**

**ANTES (Primera versión - causaba desconexiones):**
```csharp
private int maxSearchesPerMinute = 25;               // Demasiado agresivo
private const int AUTO_SEARCH_PARALLELISM_CAP = 16;  // Demasiado agresivo
```

**DESPUÉS (Versión estable - evita bans):**
```csharp
private int maxSearchesPerMinute = 15;               // 1 búsqueda cada 4s
private const int AUTO_SEARCH_PARALLELISM_CAP = 5;   // Máximo 5 simultáneas
private const int AUTO_SEARCH_MIN_PARALLELISM = 2;   // Mínimo 2 simultáneas
private int maxParallelPurgeSearches = 10;           // Reducido 50%
```

---

## 📊 Resultados Esperados

### **Antes (25/min, 16 paralelas):**
- ⚡ Velocidad: 0.3 autores/seg
- ❌ Bans temporales (5-10 min)
- 🔥 Sobrecarga del servidor

### **Después (15/min, 5 paralelas):**
- ⚡ Velocidad: ~0.15-0.2 autores/seg (más lento pero estable)
- ✅ Sin bans temporales
- 🟢 Carga sostenible

---

## 🎯 Configuración Óptima

### **Configuración Actual (Conservadora):**
- **Rate limit:** 15 búsquedas/min (1 cada 4s)
- **Búsqueda automática:** 2-5 simultáneas
- **Purga de autores:** 10 simultáneas

### **Si Siguen los Bans Temporales:**
Reduce aún más editando `MainForm.cs`:

```csharp
private int maxSearchesPerMinute = 10;               // 1 cada 6s
private const int AUTO_SEARCH_PARALLELISM_CAP = 3;   // Máximo 3
private const int AUTO_SEARCH_MIN_PARALLELISM = 1;   // Mínimo 1
```

### **Si Quieres Más Velocidad (Riesgo de Bans):**
Aumenta gradualmente:

```csharp
private int maxSearchesPerMinute = 20;               // 1 cada 3s
private const int AUTO_SEARCH_PARALLELISM_CAP = 6;   // Máximo 6
```

---

## 🔍 Monitoreo

Verifica en los logs:
```
📊 Paralelismo: 8 (rango: 3-8) | Tasa de éxito: 100,0 %
```

**Señales de problema:**
- ❌ "The underlying Tcp connection is closed"
- ❌ "DESCONEXIÓN: Unknown"
- ❌ Múltiples errores de búsqueda consecutivos

**Señales de éxito:**
- ✅ "Progreso: X/Y (Z%) | ETA: HH:MM"
- ✅ Sin errores de conexión
- ✅ Tasa de éxito > 95%

---

## 📝 Notas

1. **Trade-off:** Velocidad vs Estabilidad
   - Paralelismo alto = Más rápido pero inestable
   - Paralelismo bajo = Más lento pero estable

2. **Rust Pack 4 sigue activo:**
   - LRU Cache, Procesamiento Paralelo, Parser ID3v2
   - Las optimizaciones internas no afectan la conexión

3. **Rate limit ya optimizado:**
   - 25 búsquedas/min (3.1x más rápido que antes)
   - No requiere ajustes adicionales

---

## 🚀 Próximos Pasos

1. **Recompilar:** `compila_rapido.bat`
2. **Probar:** Ejecutar búsqueda automática
3. **Monitorear:** Verificar que no haya desconexiones
4. **Ajustar:** Si es necesario, reducir más el paralelismo

**Estado:** ✅ Cambios aplicados, listo para recompilar
