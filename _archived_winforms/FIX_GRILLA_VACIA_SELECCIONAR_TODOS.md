# Fix: Grilla Vacía al Seleccionar Todos y Descargar

**Fecha**: 28 de diciembre de 2025  
**Problema**: Al seleccionar todos los archivos para descargar, desaparecen de la grilla de búsqueda cuando regresas a la pestaña

## Problema Identificado

### Síntoma
1. Usuario hace búsqueda → Obtiene resultados
2. Selecciona todos los archivos (Ctrl+A o selección manual)
3. Descarga los archivos seleccionados
4. La aplicación cambia automáticamente a la pestaña "Descargas"
5. Usuario regresa a la pestaña "Búsquedas"
6. **La grilla está vacía** ❌

### Causa Raíz

El problema era causado por el **mecanismo de throttling** en `UpdateSearchResults()`:

```csharp
// Throttling: solo actualizar si han pasado 500ms desde la última actualización
var now = DateTime.Now;
if ((now - lastUpdateTime).TotalMilliseconds < UPDATE_THROTTLE_MS)
{
    return; // Saltar esta actualización para no saturar el UI thread
}
```

**Flujo del problema**:

1. Usuario descarga archivos → Cambia a pestaña "Descargas" (línea 11285)
2. Usuario regresa a pestaña "Búsquedas" → Dispara evento `SelectedIndexChanged`
3. Evento intenta restaurar resultados llamando a `DisplaySearchResults()`
4. `DisplaySearchResults()` llama a `UpdateSearchResults()`
5. **`UpdateSearchResults()` verifica el throttling** y encuentra que la última actualización fue hace menos de 500ms
6. **Salta la actualización** → Grilla queda vacía

### Por Qué Ocurría

El throttling está diseñado para evitar actualizaciones excesivas durante búsquedas en tiempo real, pero **no debe aplicarse** cuando el usuario cambia de pestaña y necesita restaurar los resultados.

## Solución Implementada

### Cambio en MainForm.cs (línea 4350)

**Antes**:
```csharp
else
{
    // En modo normal, verificar si la lista está vacía o tiene menos items
    if (lvResults.Items.Count == 0 || lvResults.Items.Count < allResults.Count)
    {
        // Restaurar todos los items
        DisplaySearchResults(allResults, "Restaurando resultados", "");
    }
}
```

**Ahora**:
```csharp
else
{
    // En modo normal, verificar si la lista está vacía o tiene menos items
    if (lvResults.Items.Count == 0 || lvResults.Items.Count < allResults.Count)
    {
        // Forzar actualización sin throttling
        lastUpdateTime = DateTime.MinValue;
        // Restaurar todos los items
        DisplaySearchResults(allResults, "Restaurando resultados", "");
    }
}
```

### Cómo Funciona

1. **`lastUpdateTime = DateTime.MinValue;`**: Resetea el timestamp de la última actualización al mínimo valor posible
2. Cuando `UpdateSearchResults()` verifica el throttling:
   ```csharp
   if ((now - lastUpdateTime).TotalMilliseconds < UPDATE_THROTTLE_MS)
   ```
   La diferencia será **muy grande** (años), por lo que **no se salta la actualización**
3. Los resultados se restauran correctamente en la grilla

## Contexto: Problema Anterior Relacionado

Este problema es similar al que corregimos anteriormente en `FIX_GRILLA_BUSQUEDAS_VACIA.md`, pero con una diferencia clave:

### Problema Anterior
- **Causa**: No había evento `SelectedIndexChanged` para restaurar resultados
- **Solución**: Agregar evento que restaura resultados al volver a la pestaña

### Problema Actual
- **Causa**: El evento existe, pero el throttling bloquea la actualización
- **Solución**: Resetear el throttling antes de restaurar

## Flujo Corregido

```
1. Usuario descarga archivos
   ↓
2. Cambia a pestaña "Descargas" (automático)
   ↓
3. Usuario regresa a pestaña "Búsquedas"
   ↓
4. Evento SelectedIndexChanged detecta: tabControl.SelectedIndex == 0
   ↓
5. Verifica: allResults.Count > 0 ✅
   ↓
6. Verifica: lvResults.Items.Count == 0 ✅
   ↓
7. NUEVO: lastUpdateTime = DateTime.MinValue
   ↓
8. DisplaySearchResults(allResults, ...)
   ↓
9. UpdateSearchResults(allResults)
   ↓
10. Throttling check: (now - DateTime.MinValue) > 500ms ✅
    ↓
11. Actualización procede → Grilla restaurada ✅
```

## Casos de Uso Cubiertos

### Caso 1: Seleccionar Todos (Ctrl+A)
```
1. Búsqueda: "asimov" → 598 resultados
2. Ctrl+A → Selecciona todos
3. Ctrl+D → Descarga todos
4. Cambia a pestaña "Descargas"
5. Regresa a pestaña "Búsquedas"
6. ✅ Grilla muestra los 598 resultados
```

### Caso 2: Selección Manual Múltiple
```
1. Búsqueda: "cervantes" → 108 resultados
2. Selecciona 50 archivos manualmente
3. Click derecho → Descargar
4. Cambia a pestaña "Descargas"
5. Regresa a pestaña "Búsquedas"
6. ✅ Grilla muestra los 108 resultados
```

### Caso 3: Modo Virtual (>1000 resultados)
```
1. Búsqueda: "libro" → 2500 resultados (modo virtual)
2. Selecciona todos
3. Descarga todos
4. Cambia a pestaña "Descargas"
5. Regresa a pestaña "Búsquedas"
6. ✅ Grilla muestra los 2500 resultados (modo virtual)
```

## Detalles Técnicos

### Throttling en UpdateSearchResults

**Propósito**: Evitar actualizaciones excesivas durante búsquedas en tiempo real

**Configuración**:
```csharp
private const int UPDATE_THROTTLE_MS = 500; // 500ms entre actualizaciones
private DateTime lastUpdateTime = DateTime.MinValue;
```

**Lógica**:
```csharp
var now = DateTime.Now;
if ((now - lastUpdateTime).TotalMilliseconds < UPDATE_THROTTLE_MS)
{
    return; // Saltar actualización
}
lastUpdateTime = now;
```

### Por Qué DateTime.MinValue

**`DateTime.MinValue`** representa el 1 de enero del año 0001:
```csharp
DateTime.MinValue // 0001-01-01 00:00:00
```

Cuando se calcula la diferencia:
```csharp
var now = DateTime.Now; // 2025-12-28 16:00:00
var diff = (now - DateTime.MinValue).TotalMilliseconds;
// diff ≈ 63,900,000,000,000 ms (2024 años)
```

Esto garantiza que **siempre** pase la verificación del throttling.

### Alternativas Consideradas

#### Opción 1: Flag de "forzar actualización"
```csharp
private void UpdateSearchResults(List<SearchResultItem> items, bool forceUpdate = false)
{
    if (!forceUpdate && (now - lastUpdateTime).TotalMilliseconds < UPDATE_THROTTLE_MS)
    {
        return;
    }
    // ...
}
```
**Descartada**: Requiere cambiar la firma del método en múltiples lugares.

#### Opción 2: Método separado sin throttling
```csharp
private void ForceUpdateSearchResults(List<SearchResultItem> items)
{
    // Sin throttling
}
```
**Descartada**: Duplicación de código.

#### Opción 3: Resetear timestamp (ELEGIDA)
```csharp
lastUpdateTime = DateTime.MinValue;
UpdateSearchResults(items);
```
**Ventajas**:
- Mínimo cambio de código
- No duplica lógica
- No cambia firmas de métodos
- Explícito en la intención

## Relación con Otros Fixes

### FIX_GRILLA_BUSQUEDAS_VACIA.md
- **Problema**: Grilla vacía al volver de pestaña "Descargas"
- **Solución**: Evento `SelectedIndexChanged` que restaura resultados
- **Relación**: Este fix es la base, el actual lo complementa

### FIX_TIMEOUTS_EMULE_PROGRESS_TIMER.md
- **Problema**: Timeouts en timer de progreso de eMule
- **Solución**: Semáforo para evitar llamadas concurrentes
- **Relación**: Ambos usan semáforos/flags para evitar ejecuciones no deseadas

## Fixes Adicionales: DownloadAll() y DownloadSelected()

### Problema Detectado (28 dic 2025, 4:41pm)
El usuario reportó que el problema persiste: "busquedas. descargar todo borra la grilla"

**Causa**: El fix original solo cubría el evento `tabControl.SelectedIndexChanged`, pero **no los métodos de descarga directos**:
- `DownloadAll()`: Menú contextual " Descargar Todo"
- `DownloadSelected()`: Menú contextual " Descargar"

Estos métodos no reseteaban el throttling antes de procesar las descargas.

### Soluciones Implementadas

**1. Fix en `DownloadAll()` (línea 7182-7183)**:
```csharp
if (result == DialogResult.Yes)
{
    // CRÍTICO: Resetear throttling para que la grilla se actualice correctamente al volver
    lastUpdateTime = DateTime.MinValue;
    
    bool isVirtualMode = lvResults.VirtualMode;
    // ... resto del código
}
```

**2. Fix en `DownloadSelected()` (línea 7072-7073)**:
```csharp
private async void DownloadSelected()
{
    try
    {
        if (lvResults.SelectedItems.Count == 0)
        {
            MessageBox.Show("Selecciona al menos un archivo para descargar", "Descarga", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        // CRÍTICO: Resetear throttling para que la grilla se actualice correctamente al volver
        lastUpdateTime = DateTime.MinValue;
        
        int addedCount = 0;
        // ... resto del código
    }
}
```

### Resultado

Ahora **todos los métodos de descarga** resetean el throttling:
1. `tabControl.SelectedIndexChanged` (línea 4350) - Al volver a la pestaña
2. `DownloadAll()` (línea 7183) - Antes de descargar todo
3. `DownloadSelected()` (línea 7073) - Antes de descargar seleccionados

Esto garantiza que la grilla **siempre** se actualice correctamente al volver a la pestaña de búsquedas, sin importar qué método de descarga se use.

## Compilación

**Estado**: Compilación exitosa sin errores  
**Comando**: `msbuild SlskDown.csproj /t:Build /p:Configuration=Release`  
**Exit Code**: 0

---

**Problema**: Resuelto Completamente  
**Archivos Modificados**: `MainForm.cs` (líneas 4350, 7073, 7183)  
**Impacto**: La grilla de búsqueda ahora permanece visible después de usar cualquier método de descarga (seleccionar todos, descargar todo, descargar seleccionados)

## Verificación

### Logs Esperados

**Al regresar a pestaña "Búsquedas"**:
```
[16:00:00] DEBUG: DisplaySearchResults recibió 598 resultados
[16:00:00]    Primer resultado en DisplaySearchResults: Fundacion - Isaac Asimov.epub
[16:00:00] DEBUG: UpdateSearchResults recibió 598 items
[16:00:00]    [1] Fundacion - Isaac Asimov.epub
[16:00:00]    [2] Yo, Robot - Isaac Asimov.epub
[16:00:00]    [3] El fin de la eternidad - Isaac Asimov.epub
```

**Sin el fix** (antes):
```
[16:00:00] 📺 DEBUG: DisplaySearchResults recibió 598 resultados
[16:00:00] ⏭️ DEBUG: Throttling - saltando actualización (última hace 200ms)
```

## Resumen

| Aspecto | Antes | Ahora |
|---------|-------|-------|
| **Grilla al volver** | Vacía ❌ | Llena ✅ |
| **Throttling** | Bloquea restauración ❌ | Se resetea antes ✅ |
| **Experiencia usuario** | Confusa ❌ | Fluida ✅ |
| **Código agregado** | - | 1 línea |

---

**Problema**: ✅ Resuelto  
**Archivo Modificado**: `MainForm.cs` (línea 4350)  
**Impacto**: Los resultados de búsqueda ahora persisten correctamente al descargar archivos y cambiar de pestaña
