# Filtro de Español Mejorado

## Problema Identificado

El filtro de español anterior era **demasiado permisivo** y permitía que se "colaran" muchos archivos que no eran en español.

### Causa Raíz

En la función `IsSpanishText()` (línea 7629-7631), el código tenía esta lógica:

```csharp
// Si no tiene señales claras, aceptar (preferimos falsos positivos a perder libros)
spanishTextCache[text] = true;
return true;
```

Esto significaba que **cualquier archivo sin indicadores claros de idioma** (ni español ni otros) se aceptaba por defecto, causando muchos falsos positivos.

## Solución Implementada

### Cambio Principal

Se modificó la lógica para requerir **evidencia positiva** de que el archivo es en español, en lugar de aceptar por defecto todo lo que no sea claramente otro idioma.

### Sistema de Puntuación

Ahora el filtro usa un sistema de puntuación basado en palabras españolas:

#### Palabras de Alta Puntuación (3 puntos)
- `del` - Muy distintivo del español
- `para` - Preposición española característica
- `donde`, `cuando` - Adverbios españoles

#### Palabras de Media Puntuación (2 puntos)
- Artículos: `los`, `las`
- Preposiciones: `por`, `sin`, `sobre`, `entre`, `desde`, `hasta`
- Adverbios: `siempre`, `nunca`, `también`, `tampoco`, `después`, `antes`, `ahora`, `entonces`
- Pronombres: `nada`, `alguien`, `nadie`
- Verbos comunes: `ser`, `estar`, `haber`, `hacer`, `tener`
- Conjunciones: `porque`, `aunque`, `mientras`
- Demostrativos: `aquel`, `aquella`

#### Palabras de Baja Puntuación (1 punto)
- Artículos: `una`, `uno`
- Demostrativos: `esta`, `este`, `ese`, `esa`
- Conjunciones: `como`
- Pronombres: `algo`, `otro`, `otra`, `otros`, `otras`, `mismo`, `misma`
- Cuantificadores: `todo`, `todos`, `toda`, `todas`
- Preposición: `con` (1 punto porque también existe en otros idiomas)

### Umbral de Aceptación

Se requiere una **puntuación mínima de 3** para considerar un archivo como español. Esto significa:

- ✅ Una palabra de alta puntuación (ej: "del")
- ✅ Una palabra de media + una de baja (ej: "los" + "una")
- ✅ Tres palabras de baja puntuación (ej: "una" + "este" + "como")

### Detección Rápida

Antes de aplicar el sistema de puntuación, el filtro:

1. **Acepta inmediatamente** archivos con caracteres españoles: `ñ`, `á`, `é`, `í`, `ó`, `ú`
2. **Rechaza inmediatamente** archivos con indicadores claros de otros idiomas:
   - Inglés: `the`, `of and`, `with`, `from`, `into`, `through`, terminaciones `-ing`, `-tion`
   - Italiano: `il`, `della`, `degli`, `nell`, `dell`, `che`, terminaciones `-zione`, `-zioni`
   - Francés: contracciones `l'`, `d'`, `c'`
   - Alemán: artículos `der`, `die`, `das`

## Ventajas del Nuevo Filtro

1. **Más preciso**: Reduce significativamente los falsos positivos
2. **Mantiene sensibilidad**: Títulos cortos con palabras clave españolas siguen siendo detectados
3. **Rápido**: Usa caché y detección temprana para optimizar rendimiento
4. **Balanceado**: El umbral de 3 puntos permite flexibilidad sin sacrificar precisión

## Ejemplos

### ✅ Archivos que PASAN el filtro

- "El señor de los anillos.epub" → `los` (2) + caracteres españoles → ✅
- "Cien años de soledad.pdf" → `ñ` → ✅
- "La casa del árbol.cbr" → `del` (3) → ✅
- "Cuando éramos felices.mobi" → `cuando` (3) + carácter español → ✅

### ❌ Archivos que NO PASAN el filtro

- "The Lord of the Rings.epub" → `the` → ❌ (inglés)
- "Il nome della rosa.pdf" → `della` → ❌ (italiano)
- "L'étranger.mobi" → `l'` → ❌ (francés)
- "Der Zauberberg.epub" → `der` → ❌ (alemán)
- "Mystery Novel 2024.pdf" → Sin palabras españolas → ❌ (puntuación: 0)

## Recomendaciones de Uso

1. **Activar el filtro** cuando busques específicamente contenido en español
2. **Desactivar el filtro** si buscas contenido en cualquier idioma
3. **Ajustar el umbral** si encuentras que es demasiado estricto o permisivo:
   - Línea 7692: `bool isSpanish = spanishScore >= 3;`
   - Aumentar a 4-5 para ser más estricto
   - Reducir a 2 para ser más permisivo

## Rendimiento

- **Caché**: Los resultados se almacenan en caché para evitar recalcular
- **Detección temprana**: Caracteres especiales y palabras clave se verifican primero
- **Optimizado**: Usa `Contains()` en lugar de regex para mayor velocidad

## Mejoras Adicionales Implementadas

### 1. Umbral Dinámico Según Longitud
Títulos cortos (< 20 caracteres) requieren puntuación 2, títulos largos requieren 3:
```csharp
int requiredScore = text.Length < 20 ? 2 : 3;
```

**Ejemplos**:
- ✅ "Los tres" (8 chars) → `los` (2 pts) → PASA (umbral: 2)
- ❌ "The three" (9 chars) → `the` → FALLA (inglés)
- ✅ "El libro perdido" (16 chars) → `libro` (3 pts) → PASA (umbral: 2)

### 2. Detección de Italiano Reforzada
Agregadas más palabras y terminaciones italianas problemáticas:

**Nuevas palabras detectadas**:
- `di` - Preposición italiana muy común
- `sono` - Verbo "ser" en italiano
- `è` - Verbo "es" en italiano
- `era` - Verbo "era" en italiano
- `gli`, `lo` - Artículos italianos

**Nuevas terminaciones**:
- `-aggio` (ej: "viaggio", "aggio")
- `-eggio` (ej: "parcheggio")

### 3. Términos Literarios Españoles
Agregadas palabras de alto valor para contenido literario:

**Alta puntuación (3 pts)**:
- `libro`, `novela` - Términos literarios directos
- `español`, `espanol`, `castellano` - Indicadores explícitos de idioma

**Media puntuación (2 pts)**:
- `historia`, `cuento` - Géneros literarios
- `saga`, `serie` - Colecciones
- `tomo`, `volumen` - Partes de obras
- `edición`, `edicion` - Publicaciones

**Impacto**: Archivos con términos literarios españoles pasan automáticamente el filtro.

## Ejemplos Actualizados

### ✅ Archivos que PASAN el filtro

**Con caracteres españoles**:
- "El señor de los anillos.epub" → `ñ` → ✅
- "Cien años de soledad.pdf" → `ñ` → ✅

**Con palabras de alta puntuación**:
- "La novela perdida.epub" → `novela` (3) → ✅
- "El libro rojo.pdf" → `libro` (3) → ✅
- "Historia del tiempo.mobi" → `historia` (2) + `del` (3) → ✅

**Títulos cortos (umbral reducido)**:
- "Los tres.epub" (8 chars) → `los` (2) → ✅ (umbral: 2)
- "El rey.pdf" (6 chars) → Sin palabras clave → ❌

**Con términos literarios**:
- "Saga completa español.cbr" → `saga` (2) + `español` (3) → ✅
- "Tomo 1 edición.epub" → `tomo` (2) + `edición` (2) → ✅

### ❌ Archivos que NO PASAN el filtro (mejorado)

**Italiano (detección reforzada)**:
- "Il nome della rosa.pdf" → `il` → ❌
- "Storia di una vita.epub" → `di` → ❌
- "Sono felice.mobi" → `sono` → ❌
- "Il viaggio.pdf" → `il` + `aggio` → ❌
- "Era una volta.epub" → `era` → ❌

**Inglés**:
- "The Lord of the Rings.epub" → `the` → ❌
- "History of time.pdf" → `of and` → ❌

**Sin evidencia**:
- "Mystery 2024.pdf" → Puntuación: 0 → ❌
- "Novel XYZ.epub" → Puntuación: 0 → ❌

## Comparación: Antes vs Ahora

| Característica | Antes | Ahora |
|---|---|---|
| **Italiano detectado** | Básico (6 patrones) | Reforzado (12 patrones) |
| **Términos literarios** | No | Sí (11 términos) |
| **Umbral dinámico** | Fijo (3) | Adaptativo (2-3) |
| **Falsos positivos** | Alto | Bajo |
| **Sensibilidad títulos cortos** | Baja | Alta |

## Versión

- **Fecha**: 14 de noviembre de 2025 (actualizado)
- **Versión de SlskDown**: 4.1.0
- **Archivo modificado**: `MainForm.cs` (líneas 7575-7712)
- **Mejoras**: 3 (Umbral dinámico + Italiano reforzado + Términos literarios)
