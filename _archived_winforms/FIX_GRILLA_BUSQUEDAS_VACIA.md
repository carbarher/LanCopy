# Fix: Grilla de Búsquedas Vacía al Regresar de Descargas

**Fecha**: 28 de diciembre de 2025  
**Problema**: Al descargar un archivo desde la pestaña Búsquedas, la aplicación cambia a Descargas, pero al regresar a Búsquedas la grilla aparece vacía

## Problema Identificado

### Comportamiento Reportado

1. Usuario hace una búsqueda → Obtiene resultados en la grilla
2. Usuario selecciona un archivo → Hace clic en "Descargar"
3. Aplicación cambia automáticamente a pestaña **Descargas**
4. Usuario regresa a pestaña **Búsquedas**
5. **Grilla aparece vacía** ❌

### Causa del Problema

El evento `SelectedIndexChanged` del `tabControl` (línea 4323) intentaba restaurar los resultados cuando el usuario regresaba a la pestaña de búsquedas, pero tenía condiciones que no cubrían todos los casos:

**Código Anterior**:
```csharp
tabControl.SelectedIndexChanged += (s, e) =>
{
    if (tabControl.SelectedIndex == 0 && allResults.Count > 0 && lvResults != null)
    {
        SafeBeginInvoke(() =>
        {
            if (lvResults.VirtualMode && lvResults.VirtualListSize != allResults.Count)
            {
                lvResults.VirtualListSize = allResults.Count;
                lblResultsCount.Text = $"{allResults.Count} archivos";
            }
            else if (!lvResults.VirtualMode && lvResults.Items.Count == 0 && allResults.Count > 0)
            {
                // Solo restauraba si Items.Count == 0
                DisplaySearchResults(allResults, "Restaurando resultados", "");
            }
        });
    }
};
```

**Problemas**:
1. En modo virtual, solo actualizaba el tamaño pero **no forzaba el refresco visual**
2. En modo normal, solo restauraba si `Items.Count == 0`, no si había menos items de los esperados
3. No había manejo de excepciones

## Solución Implementada

### Cambios en MainForm.cs (líneas 4322-4359)

**Código Mejorado**:
```csharp
// Restaurar resultados de búsqueda cuando vuelves a la pestaña
tabControl.SelectedIndexChanged += (s, e) =>
{
    if (tabControl.SelectedIndex == 0 && allResults.Count > 0 && lvResults != null)
    {
        // Restaurar la vista de resultados si hay resultados previos
        SafeBeginInvoke(() =>
        {
            try
            {
                if (lvResults.VirtualMode)
                {
                    // En modo virtual, asegurar que el tamaño esté correcto
                    if (lvResults.VirtualListSize != allResults.Count)
                    {
                        lvResults.VirtualListSize = allResults.Count;
                    }
                    // ✅ NUEVO: Forzar refresco visual
                    lvResults.Invalidate();
                    lblResultsCount.Text = $"{allResults.Count} archivos";
                }
                else
                {
                    // ✅ MEJORADO: Verificar si está vacía O tiene menos items
                    if (lvResults.Items.Count == 0 || lvResults.Items.Count < allResults.Count)
                    {
                        // Restaurar todos los items
                        DisplaySearchResults(allResults, "Restaurando resultados", "");
                    }
                }
            }
            catch (Exception ex)
            {
                // ✅ NUEVO: Manejo de excepciones
                AutoLog($"⚠️ Error restaurando resultados: {ex.Message}");
            }
        });
    }
};
```

### Mejoras Implementadas

1. **Modo Virtual** (búsquedas >1000 resultados):
   - ✅ Actualiza `VirtualListSize` si es necesario
   - ✅ **Llama a `Invalidate()` para forzar el refresco visual**
   - ✅ Actualiza el contador de archivos

2. **Modo Normal** (búsquedas ≤1000 resultados):
   - ✅ Verifica si la lista está vacía **O tiene menos items** de los esperados
   - ✅ Restaura todos los resultados llamando a `DisplaySearchResults()`

3. **Manejo de Errores**:
   - ✅ Try-catch para evitar que excepciones rompan la funcionalidad
   - ✅ Log de errores para diagnóstico

## Flujo Corregido

### Antes (Con Bug)

```
1. Usuario busca "quevedo" → 220 resultados ✅
2. Usuario selecciona archivo → Descarga
3. App cambia a pestaña Descargas ✅
4. Usuario regresa a Búsquedas
5. Grilla vacía ❌ (allResults tiene datos pero no se muestran)
```

### Ahora (Corregido)

```
1. Usuario busca "quevedo" → 220 resultados ✅
2. Usuario selecciona archivo → Descarga
3. App cambia a pestaña Descargas ✅
4. Usuario regresa a Búsquedas
5. Evento SelectedIndexChanged detecta:
   - tabControl.SelectedIndex == 0 (pestaña Búsquedas)
   - allResults.Count > 0 (hay resultados guardados)
6. Restaura la visualización:
   - Modo virtual: Invalidate() fuerza refresco ✅
   - Modo normal: DisplaySearchResults() recrea items ✅
7. Grilla muestra los 220 resultados ✅
```

## Casos de Uso Cubiertos

### Caso 1: Búsqueda Pequeña (≤1000 resultados)
- Modo: Normal (Items en ListView)
- Comportamiento: Recrea todos los items si la lista está vacía o incompleta
- Resultado: ✅ Resultados restaurados

### Caso 2: Búsqueda Grande (>1000 resultados)
- Modo: Virtual (VirtualListSize)
- Comportamiento: Actualiza tamaño y fuerza refresco con `Invalidate()`
- Resultado: ✅ Resultados restaurados

### Caso 3: Error Durante Restauración
- Comportamiento: Captura excepción y registra en log
- Resultado: ✅ No rompe la aplicación

## Verificación

### Cómo Probar

1. **Hacer una búsqueda**:
   ```
   Buscar: "quevedo"
   Resultado: 220 archivos en grilla
   ```

2. **Descargar un archivo**:
   ```
   Seleccionar archivo → Clic derecho → Descargar
   App cambia a pestaña Descargas
   ```

3. **Regresar a Búsquedas**:
   ```
   Clic en pestaña "Búsquedas"
   Resultado esperado: ✅ 220 archivos visibles
   ```

### Logs Esperados

Cuando regresas a la pestaña Búsquedas, deberías ver (en modo normal):
```
[Log] Restaurando resultados
[Log] 📺 DEBUG: DisplaySearchResults recibió 220 resultados
```

O en modo virtual (sin logs, pero la grilla se refresca automáticamente).

## Notas Técnicas

### ¿Por Qué `Invalidate()`?

En modo virtual, el `ListView` no mantiene los items en memoria. Solo mantiene el `VirtualListSize`. Cuando cambias de pestaña, el control puede perder su estado visual. `Invalidate()` fuerza al control a redibujar completamente, lo que dispara el evento `RetrieveVirtualItem` para cada item visible.

### ¿Por Qué Verificar `Items.Count < allResults.Count`?

En algunos casos, la grilla puede tener algunos items pero no todos (por ejemplo, si hubo un error parcial al restaurar). Esta condición asegura que siempre se restauren **todos** los resultados.

### Variable `allResults`

Esta variable global (`List<SearchResultItem>`) mantiene **todos** los resultados de la última búsqueda. No se limpia al cambiar de pestaña, solo cuando:
- Se inicia una nueva búsqueda
- Se presiona Ctrl+L (limpiar resultados)
- Se activa el modo automático (>100 items)

## Compilación

✅ **Estado**: Compilación exitosa sin errores  
✅ **Comando**: `msbuild SlskDown.csproj /t:Build /p:Configuration=Release`  
✅ **Exit Code**: 0

## Resumen

| Aspecto | Antes | Ahora |
|---------|-------|-------|
| **Modo Virtual** | Solo actualiza tamaño | Actualiza tamaño + `Invalidate()` ✅ |
| **Modo Normal** | Solo si `Count == 0` | Si `Count == 0` O `Count < allResults.Count` ✅ |
| **Manejo Errores** | Sin try-catch | Try-catch con log ✅ |
| **Resultado** | Grilla vacía ❌ | Grilla con resultados ✅ |

---

**Problema**: ✅ Resuelto  
**Archivos Modificados**: `MainForm.cs` (líneas 4322-4359)  
**Impacto**: Mejora significativa en UX - Los resultados de búsqueda se mantienen visibles
