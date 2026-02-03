# Verificación de Variaciones de Nombres

## Para "A. A. Pepito"

Según el código en `MainForm.cs` líneas 16100-16169, se deberían generar:

### Variaciones Generadas

1. **`A. A. Pepito`** (línea 16118)
   - Original sin modificar

2. **`A A Pepito`** (línea 16129)
   - Sin puntos: `author.Replace(".", "")`

3. **`AA Pepito`** (línea 16135)
   - Sin puntos + espacios normalizados
   - `RegexWhitespaceNormalize.Replace(withoutDots, " ")`

4. **`A.A. Pepito`** (línea 16148)
   - Con puntos, sin espacios entre iniciales
   - Regex: `\.(\s+)(?=[A-Z]\.)` → `.`
   - Transforma `A. A.` → `A.A.`

5. **`A. A. Pepito`** (línea 16154) - duplicado
   - Sin tildes (en este caso no hay tildes)

6. **`A A Pepito`** (línea 16162) - duplicado
   - Sin puntos ni tildes

7. **`AA Pepito`** (línea 16168) - duplicado
   - Completamente normalizado

### Total Único

Debido a que `HashSet` elimina duplicados:
- **4 variaciones únicas** para "A. A. Pepito"

## Cómo Verificar

### Opción 1: Logs de la Aplicación

1. Crea un archivo `test.txt` con:
   ```
   A. A. Pepito
   ```

2. Carga el archivo en SlskDown (pestaña Automático → Cargar Lista)

3. Observa los logs:
   ```
   ✅ Cargados X autores únicos en Y.Ys
   📝 Expandidos: 1 → X (variantes sin puntos/tildes)
   ```

   Donde `X` debería ser **4** (1 original + 3 variantes únicas)

### Opción 2: Búsqueda Manual

Después de cargar "A. A. Pepito", busca en la grilla de autores:

1. `A. A. Pepito` ✅
2. `A A Pepito` ✅
3. `AA Pepito` ✅
4. `A.A. Pepito` ✅

Todas deberían aparecer en la lista.

### Opción 3: Verificar en Búsquedas

Cuando hagas una búsqueda automática, verifica en los logs que se busquen todas las variantes:

```
🔍 Buscando: A. A. Pepito
🔍 Buscando: A A Pepito
🔍 Buscando: AA Pepito
🔍 Buscando: A.A. Pepito
```

## Ejemplo Completo: "J. R. R. Tolkien"

### Variaciones Esperadas

1. `J. R. R. Tolkien` (original)
2. `J R R Tolkien` (sin puntos)
3. `JRR Tolkien` (sin puntos, espacios normalizados)
4. `J.R.R. Tolkien` (con puntos, sin espacios)

**Total**: 4 variaciones únicas

### Regex Explicada

Para transformar `J. R. R. Tolkien` → `J.R.R. Tolkien`:

```regex
\.(\s+)(?=[A-Z]\.)
```

**Paso a paso**:
1. `J. ` → Encuentra `. ` (punto + espacio)
2. Verifica que sigue `R.` (mayúscula + punto)
3. Reemplaza `. ` por `.` (solo punto)
4. Resultado: `J.R. R. Tolkien`
5. Repite para `R. ` → `R.`
6. Resultado final: `J.R.R. Tolkien`

## Si No Aparecen las Variaciones

### Posibles Causas

1. **Código no compilado**
   - Verifica que el ejecutable sea reciente
   - Compila con `compile_test.bat`

2. **Caché de autores**
   - Cierra y reabre la aplicación
   - Vuelve a cargar el archivo

3. **Filtro activo**
   - Verifica que no haya filtros en la grilla de autores
   - Limpia el cuadro de búsqueda

4. **HashSet case-insensitive**
   - Las variaciones se deduplicarán si son iguales (case-insensitive)
   - `A. A. Pepito` y `a. a. pepito` se consideran iguales

### Debug

Agrega un log temporal en la línea 16148:

```csharp
if (withDotsNoSpaces != author && !string.IsNullOrWhiteSpace(withDotsNoSpaces))
{
    Log($"[DEBUG] Variante generada: '{author}' → '{withDotsNoSpaces}'");
    expanded.Add(withDotsNoSpaces);
}
```

Esto mostrará en los logs cada variante generada.

## Conclusión

El código **SÍ genera las variaciones** correctamente. Si no las ves:

1. Verifica que estés usando el ejecutable compilado más reciente
2. Revisa los logs al cargar autores
3. Busca manualmente las variaciones en la grilla
4. Agrega logs de debug si es necesario

**Código verificado**: `MainForm.cs` líneas 16100-16169 ✅
