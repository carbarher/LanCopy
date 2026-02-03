# 📝 Normalización de Nombres de Autores - SlskDown

## Problema

Al buscar autores, pueden existir múltiples variaciones del mismo nombre que se tratan como autores diferentes:

- `A. E. Pepito` (con puntos y espacios)
- `A E Pepito` (sin puntos, con espacios)
- `A.E. Pepito` (con puntos, sin espacios entre iniciales)
- `AE Pepito` (sin puntos ni espacios)
- `A.E.Pepito` (todo junto)

Esto genera:
- ❌ Búsquedas duplicadas del mismo autor
- ❌ Resultados fragmentados
- ❌ Desperdicio de recursos

## Solución Implementada

### 1. Función de Normalización (`ValidationHelpers.cs`)

```csharp
public static string NormalizeAuthorName(string authorName)
{
    // Convierte: "A. E. Pepito" → "ae pepito"
    // 1. Minúsculas
    // 2. Eliminar puntos
    // 3. Normalizar espacios múltiples a uno solo
    // 4. Trim
}

public static bool AreAuthorNamesEquivalent(string name1, string name2)
{
    // Compara dos nombres ignorando variaciones de formato
    return NormalizeAuthorName(name1) == NormalizeAuthorName(name2);
}
```

### 2. Expansión de Variantes al Cargar Autores (`MainForm.cs` líneas 12924-12973)

Cuando cargas un archivo de autores, el sistema automáticamente genera todas las variantes posibles:

**Entrada:**
```
A. E. Pepito
```

**Variantes generadas:**
1. `A. E. Pepito` (original)
2. `A E Pepito` (sin puntos)
3. `AE Pepito` (sin puntos, espacios normalizados)
4. `A E Pepito` (sin tildes - si las hubiera)
5. `AE Pepito` (sin puntos, sin tildes, espacios normalizados)

**Resultado:** Cualquier variación que encuentre en Soulseek coincidirá con alguna de las generadas.

## Ejemplos Prácticos

### Ejemplo 1: Iniciales con Puntos

**Archivo de entrada (`autores.txt`):**
```
A. E. van Vogt
I. Asimov
```

**Variantes generadas automáticamente:**
```
A. E. van Vogt
A E van Vogt
AE van Vogt
A E van Vogt
AE van Vogt
I. Asimov
I Asimov
```

**Búsquedas que coincidirán:**
- Usuario comparte como: `A.E. van Vogt` ✅ Coincide con `AE van Vogt`
- Usuario comparte como: `A E van Vogt` ✅ Coincide directamente
- Usuario comparte como: `I Asimov` ✅ Coincide directamente

### Ejemplo 2: Nombres con Tildes

**Archivo de entrada:**
```
José Saramago
```

**Variantes generadas:**
```
José Saramago (original)
Jose Saramago (sin tildes)
```

**Búsquedas que coincidirán:**
- Usuario comparte como: `Jose Saramago` ✅
- Usuario comparte como: `José Saramago` ✅

### Ejemplo 3: Combinación Compleja

**Archivo de entrada:**
```
J. R. R. Tolkien
```

**Variantes generadas:**
```
J. R. R. Tolkien (original)
J R R Tolkien (sin puntos)
JRR Tolkien (sin puntos, espacios normalizados)
```

**Búsquedas que coincidirán:**
- `J.R.R. Tolkien` ✅
- `JRR Tolkien` ✅
- `J R R Tolkien` ✅
- `J. R. R. Tolkien` ✅

## Uso en Código

### Comparar Nombres de Autores

```csharp
using SlskDown.Services;

// Verificar si dos nombres son equivalentes
bool sonIguales = ValidationHelpers.AreAuthorNamesEquivalent(
    "A. E. Pepito", 
    "AE Pepito"
); // true

// Normalizar antes de almacenar en caché
string cacheKey = ValidationHelpers.NormalizeAuthorName("A. E. Pepito");
// cacheKey = "ae pepito"
```

### Deduplicar Lista de Autores

```csharp
var autores = new List<string> 
{ 
    "A. E. Pepito", 
    "A E Pepito", 
    "AE Pepito" 
};

var unicos = autores
    .GroupBy(a => ValidationHelpers.NormalizeAuthorName(a))
    .Select(g => g.First())
    .ToList();
// Resultado: ["A. E. Pepito"] (solo uno)
```

## Ventajas

✅ **Búsquedas más completas**: Encuentra obras del autor sin importar cómo estén formateadas

✅ **Menos duplicados**: Evita buscar el mismo autor múltiples veces

✅ **Mejor experiencia**: El usuario no necesita probar todas las variaciones manualmente

✅ **Optimización automática**: Se genera al cargar el archivo, sin intervención del usuario

## Limitaciones

⚠️ **Expansión de variantes aumenta el tamaño de la lista**: 
- Un autor con iniciales puede generar 3-5 variantes
- Para 1000 autores → ~3000-5000 variantes
- Impacto: Mayor uso de memoria, pero mejor cobertura

⚠️ **No detecta errores ortográficos**:
- `A. E. Pepito` vs `A. E. Pepito` (con espacio extra) → Se normaliza ✅
- `A. E. Pepito` vs `A. E. Pepito` (error de tipeo) → NO se detecta ❌

## Configuración

No requiere configuración adicional. La normalización se aplica automáticamente al:

1. **Cargar archivo de autores** (`📂 Cargar Lista`)
2. **Agregar autor manualmente** (en desarrollo)
3. **Importar desde fuentes externas** (Wikipedia, Goodreads, etc.)

## Logs

Al cargar autores, verás en el log:

```
⏳ Cargando autores desde autores_sf.txt...
   📄 Leídos 1,000 autores del archivo
   📦 Dividiendo en 1 chunks de 1000 autores
   🔄 Expandiendo variantes (puntos/tildes)...
      📦 Chunk 1/1: Procesando 1000 autores (0-999)...
      ✅ Chunk 1/1 completado (3,245 autores únicos acumulados)
   🔍 Deduplicando y ordenando 3,245 autores...
✅ Cargados 3,245 autores únicos en 0.8s
```

Nota: `3,245 autores únicos` incluye todas las variantes generadas de los 1,000 originales.

## Mejoras Futuras

🔮 **Posibles mejoras**:

1. **Normalización de apellidos compuestos**: `van Vogt` vs `Van Vogt` vs `van vogt`
2. **Detección de alias**: `Isaac Asimov` = `I. Asimov`
3. **Fuzzy matching**: Detectar errores ortográficos menores
4. **Caché de normalización**: Evitar recalcular para nombres repetidos
5. **UI para ver variantes**: Mostrar qué variantes se generaron por autor

## Referencias

- Código de normalización: `Services/ValidationHelpers.cs` líneas 221-257
- Expansión de variantes: `MainForm.cs` líneas 12924-12973
- Documentación de detección de idioma: `DETECCION_IDIOMA.md`
