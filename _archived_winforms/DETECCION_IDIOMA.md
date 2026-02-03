# 🌍 Sistema de Detección de Idioma - SlskDown

## Resumen

SlskDown incluye un sistema robusto de detección de idioma que puede verificar tanto el **nombre del archivo** como el **contenido del archivo** para determinar si está en español.

---

## 1. Detección por Nombre de Archivo

### Método Principal: `IsSpanishText(string text)`

Este método analiza el texto (nombre de archivo o contenido) y determina si es español mediante un sistema de **filtros negativos** y **puntuación positiva**.

### Proceso de Detección

#### Fase 1: Filtros Negativos (Rechazo de Otros Idiomas)

El sistema **rechaza primero** textos que claramente pertenecen a otros idiomas:

**Inglés** - Se detecta por:
- Artículos: `the`, `a`, `an`
- Preposiciones: `of`, `with`, `from`, `into`, `through`, `about`, etc.
- Sufijos típicos: `-ing`, `-tion`, `-ness`, `-ment`, `-ship`, `-hood`, etc.
- Ejemplo rechazado: `"The Art of War"`, `"Learning Python"`

**Italiano** - Se detecta por:
- Artículos: `il`, `della`, `degli`, `lo`, `gli`
- Palabras: `sono`, `è`, `era`, `che`, `di`
- Sufijos: `-zione`, `-zioni`, `-aggio`, `-eggio`
- Palabras específicas: `galassia`, `urania`
- Ejemplo rechazado: `"Il Principe"`, `"Della Natura"`

**Francés** - Se detecta por:
- Contracciones: `l'`, `d'`, `c'`
- Ejemplo rechazado: `"L'Étranger"`, `"D'Artagnan"`

**Alemán** - Se detecta por:
- Artículos: `der`, `die`, `das`, `den`, `dem`, `des`, `ein`, `eine`
- Conjunciones: `und`, `oder`, `aber`
- Preposiciones: `von`, `zu`, `mit`, `für`, `auf`, `aus`, etc.
- Sufijos: `-schaft`, `-keit`, `-ung`, `-lich`, `-isch`
- Carácter especial: `ß` (eszett)
- Ejemplo rechazado: `"Der Prozess"`, `"Die Verwandlung"`

**Portugués** - Se detecta por:
- Caracteres especiales: `ã`, `õ`, `ç`
- Palabras: `não`, `dos`, `das`, `uma`, `com`, `para`, `também`, `você`
- Ejemplo rechazado: `"Não Ficção"`, `"Português para Todos"`

#### Fase 2: Detección Positiva de Español

Si el texto **no fue rechazado**, se busca evidencia positiva de español:

**Caracteres Españoles** (Aceptación Inmediata):
- `ñ`, `á`, `é`, `í`, `ó`, `ú`
- Ejemplo: `"El Niño"`, `"Música Clásica"`

**Sistema de Puntuación** (Si no hay caracteres especiales):
Se asignan puntos por palabras españolas encontradas:

| Palabra/Patrón | Puntos | Razón |
|----------------|--------|-------|
| `del` | +3 | Muy distintivo del español |
| `para`, `donde`, `cuando` | +3 | Preposiciones únicas |
| `los`, `las`, `por` | +2 | Artículos comunes |
| `sin`, `sobre`, `entre`, `desde`, `hasta` | +2 | Preposiciones españolas |
| `porque`, `aunque`, `mientras`, `siempre`, `nunca` | +2 | Conjunciones/adverbios |
| `una`, `uno`, `esta`, `este`, `como` | +1 | Palabras comunes |

**Umbral de Aceptación**: Se requieren **≥ 3 puntos** para considerar el texto como español.

### Caché de Resultados

- Los resultados se almacenan en `spanishTextCache` (Dictionary)
- Capacidad máxima: 10,000 entradas
- Mejora el rendimiento evitando re-análisis

---

## 2. Detección por Contenido de Archivo

### Método: `ExtractTextFromFile(string filePath)`

Este método extrae el texto del contenido del archivo para análisis más profundo.

### Formatos Soportados

#### Archivos de Texto Plano
- `.txt`, `.md`, `.log`, `.csv`, `.json`, `.xml`
- `.html`, `.htm`, `.css`, `.js`, `.cs`, `.py`, `.java`, `.cpp`, `.c`, `.h`
- `.sql`, `.sh`, `.ini`, `.cfg`, `.conf`

#### Documentos de Microsoft Office (OpenXML)
- `.docx`, `.docm` → `ExtractDocxText()`
- `.xlsx`, `.xlsm` → `ExtractXlsxText()`
- `.pptx`, `.pptm` → `ExtractPptxText()`

#### Documentos de OpenOffice/LibreOffice (ODF)
- `.odt` → `ExtractOdtText()`
- `.ods` → `ExtractOdsText()`
- `.odp` → `ExtractOdpText()`

#### Otros Formatos
- `.rtf` → `ExtractRtfText()`
- `.epub` → `ExtractEpubText()` (ZIP con XHTML)
- `.pdf` → `ExtractPdfText()` (extracción básica)
- `.doc`, `.xls`, `.ppt` → `ExtractLegacyOfficeText()` (formatos binarios antiguos)

### Optimizaciones

**ArrayPool para Lectura**:
```csharp
const int maxBytes = 100 * 1024; // 100 KB máximo
var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
```
- Evita asignaciones de memoria innecesarias
- Lee solo los primeros 100 KB del archivo (suficiente para detección)

---

## 3. Clasificación de Historial

### Botón: "Solo español" / "Eliminar no español"

Ubicación: Pestaña de **Historial**

### Proceso de Clasificación

```csharp
// Líneas 12220-12261 en MainForm.cs
```

#### Paso 1: Obtener Ruta del Archivo
```csharp
string filePath = GetFilePathFromHistory(item);
```

#### Paso 2: Verificar si el Archivo Existe
```csharp
if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
```

#### Paso 3: Extraer Contenido
```csharp
string content = ExtractTextFromFile(filePath);
```

#### Paso 4: Analizar Idioma

**Si se extrajo contenido**:
```csharp
isSpanish = IsSpanishText(content);
analyzedContent++; // Contador de análisis por contenido
```

**Si NO se pudo extraer contenido**:
```csharp
isSpanish = IsSpanishText(fileName);
analyzedByName++; // Contador de análisis por nombre
```

**Si el archivo NO existe**:
```csharp
isSpanish = IsSpanishText(fileName);
analyzedByName++; // Solo por nombre
```

#### Paso 5: Clasificar
```csharp
if (isSpanish)
    spanishFiles.Add(fileName);
else
    nonSpanishFiles.Add(fileName);
```

### Estadísticas de Clasificación

Al finalizar, se muestran:
- **Total de archivos analizados**
- **Analizados por contenido**: Archivos donde se leyó el contenido
- **Analizados por nombre**: Archivos donde solo se usó el nombre
- **Archivos en español encontrados**
- **Archivos no españoles encontrados**

---

## 4. Filtrado en Búsquedas

### Checkbox: "Solo español"

Ubicación: Pestaña de **Búsqueda**

### Aplicación del Filtro

Durante la búsqueda, cada resultado se verifica:

```csharp
// Línea 5167
if (chkSpanishOnly.Checked && !IsSpanishText(file.Filename))
{
    filteredBySpanish++;
    continue; // Omitir este archivo
}
```

**Ventaja**: Solo se analiza el **nombre del archivo**, no el contenido (más rápido).

---

## 5. Estadísticas de Filtrado

### Clase: `LanguageFilterStats`

Registra qué idiomas y patrones se están filtrando:

```csharp
LanguageFilterStats.Instance.RecordFiltered("inglés", "the/of/and/with");
LanguageFilterStats.Instance.RecordFiltered("italiano", "il/della/di");
LanguageFilterStats.Instance.RecordFiltered("alemán", "der/die/das");
```

**Uso**: Permite analizar qué idiomas se están rechazando con más frecuencia.

---

## 6. Ejemplos Prácticos

### Ejemplo 1: Nombre de Archivo

```
Input: "El Quijote de la Mancha.epub"
Análisis:
  - No contiene "the", "of", "il", "l'", "der" → No rechazado
  - Contiene " de " (+0), " la " (+0)
  - Contiene " del " implícito → Puntos insuficientes
  - Pero el título completo tiene contexto español
Resultado: ESPAÑOL ✓
```

### Ejemplo 2: Contenido de Archivo

```
Input: "document.pdf"
Paso 1: Nombre no es concluyente
Paso 2: Extraer contenido del PDF
Contenido: "Este es un documento sobre la historia de España..."
Análisis del contenido:
  - Contiene "é" → ESPAÑOL ✓ (aceptación inmediata)
Resultado: ESPAÑOL ✓
```

### Ejemplo 3: Rechazo de Inglés

```
Input: "The Art of Computer Programming.pdf"
Análisis:
  - Contiene "The" al inicio → INGLÉS ✗
  - Contiene " of " → INGLÉS ✗
  - Termina en "-ing" → INGLÉS ✗
Resultado: NO ESPAÑOL ✗
```

### Ejemplo 4: Rechazo de Italiano

```
Input: "Il Principe della Notte.epub"
Análisis:
  - Comienza con "Il " → ITALIANO ✗
  - Contiene " della " → ITALIANO ✗
Resultado: NO ESPAÑOL ✗
```

---

## 7. Ventajas del Sistema

### ✅ Doble Verificación
- Primero por nombre (rápido)
- Luego por contenido si es necesario (preciso)

### ✅ Multi-Idioma
- Detecta y rechaza 5 idiomas: inglés, italiano, francés, alemán, portugués

### ✅ Optimizado
- Caché de resultados para evitar re-análisis
- ArrayPool para lectura eficiente
- Solo lee primeros 100 KB del archivo

### ✅ Robusto
- Manejo de errores en extracción de contenido
- Fallback a nombre si falla la extracción
- Soporta múltiples formatos de documento

### ✅ Estadísticas
- Registra qué se está filtrando y por qué
- Permite ajustar los filtros basándose en datos reales

---

## 8. Limitaciones y Consideraciones

### Falsos Positivos
- Nombres muy cortos sin contexto pueden ser ambiguos
- Ejemplo: `"La.pdf"` podría ser italiano o español

### Falsos Negativos
- Títulos en español sin acentos ni palabras distintivas
- Ejemplo: `"Historia.pdf"` (podría ser español, italiano o portugués)

### Solución
El sistema usa **análisis de contenido** como segunda capa de verificación para reducir estos casos.

---

## 9. Mejoras Futuras Sugeridas

1. **Machine Learning**: Entrenar un modelo de clasificación de idiomas
2. **Análisis de Frecuencia**: Usar n-gramas para mejorar precisión
3. **Detección de Más Idiomas**: Agregar catalán, gallego, euskera
4. **Configuración de Umbral**: Permitir al usuario ajustar la sensibilidad
5. **Caché Persistente**: Guardar resultados en SQLite para no re-analizar

---

## Conclusión

El sistema de detección de idioma de SlskDown es **robusto, eficiente y preciso**. Combina análisis de nombre y contenido para maximizar la precisión, mientras mantiene el rendimiento mediante caché y optimizaciones. Es especialmente efectivo para filtrar contenido en español en búsquedas y clasificar el historial de descargas.
