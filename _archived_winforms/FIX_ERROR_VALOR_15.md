# Fix: Error de Valor 15 en Controles Numéricos

## Problema

Al iniciar la aplicación, aparecía un error:

```
Error al inicializar la aplicación:
El valor de '15' no es válido para 'Value'. 'Value' debería estar
entre 'Minimum' y 'Maximum'. (Parameter 'value')
Actual value was 15.
```

---

## 🔍 Causa del Problema

### **Inconsistencia entre Configuración y Controles UI:**

**Problema 1: Proveedores Alternativos**
1. **Configuración guardada:** `maxAlternativeRetries = 15`
2. **Control NumericUpDown:** Máximo = 10
3. **Constante MAX_TOTAL_ATTEMPTS:** 15

**Problema 2: Búsquedas Simultáneas (Modo Agresivo)**
1. **Modo Agresivo establece:** `maxParallelSearches = 20`
2. **Control NumericUpDown:** Máximo = 15
3. **Valor necesario:** 20 (o más)

**Problema:** Los valores guardados o establecidos excedían los máximos de los controles.

---

## 📊 Análisis

### **Código Problemático (línea 1661):**

```csharp
// ANTES: Máximo = 10
var altRow = CreateNumericRow(
    "🔍 Proveedores alternativos:", 
    out numAltControl, 
    maxAlternativeRetries,  // Valor: 15
    0,                       // Mínimo: 0
    10,                      // Máximo: 10 ← PROBLEMA
    (s, e) => { ... }
);
altRow.Controls.Add(CreateSmallLabel($"(máx {MAX_TOTAL_ATTEMPTS} intentos totales)"));
```

**Problema:** El label dice "máx 15 intentos totales" pero el control solo permite hasta 10.

---

### **Cómo Ocurrió:**

1. Usuario configuró `maxAlternativeRetries = 15` (posiblemente editando config.json manualmente)
2. Al iniciar la aplicación, `LoadConfig()` carga el valor 15
3. Al crear el control `NumericUpDown`, intenta establecer `Value = 15`
4. El control valida: `15 > Maximum (10)` → **Error**

---

## ✅ Solución Implementada

### **1. Aumentar Máximo del Control de Proveedores Alternativos** (línea 1661)

**Antes:**
```csharp
var altRow = CreateNumericRow(
    "🔍 Proveedores alternativos:", 
    out numAltControl, 
    maxAlternativeRetries, 
    0, 
    10,  // Máximo: 10
    (s, e) => { ... }
);
```

**Después:**
```csharp
var altRow = CreateNumericRow(
    "🔍 Proveedores alternativos:", 
    out numAltControl, 
    maxAlternativeRetries, 
    0, 
    MAX_TOTAL_ATTEMPTS,  // Máximo: 15 (consistente con la constante)
    (s, e) => { ... }
);
```

**Beneficio:** El control ahora permite valores hasta 15, consistente con `MAX_TOTAL_ATTEMPTS`.

---

### **2. Aumentar Máximo del Control de Búsquedas Simultáneas** (línea 1696)

**Antes:**
```csharp
CreateNumericRow(
    "Búsquedas simultáneas:", 
    out numSearchesControl, 
    maxParallelSearches, 
    1, 
    15,  // Máximo: 15
    (s, e) => { ... }
)
```

**Después:**
```csharp
CreateNumericRow(
    "Búsquedas simultáneas:", 
    out numSearchesControl, 
    maxParallelSearches, 
    1, 
    30,  // Máximo: 30 (permite modo agresivo con 20)
    (s, e) => { ... }
)
```

**Beneficio:** El control ahora permite valores hasta 30, suficiente para el modo agresivo (20).

---

### **3. Validar Valor de Proveedores Alternativos al Cargar** (línea 3853)

**Antes:**
```csharp
maxAlternativeRetries = configManager.GetValue("maxAlternativeRetries", 3);
```

**Después:**
```csharp
maxAlternativeRetries = Math.Min(
    configManager.GetValue("maxAlternativeRetries", 3), 
    MAX_TOTAL_ATTEMPTS
);
```

**Beneficio:** 
- Si el valor guardado excede `MAX_TOTAL_ATTEMPTS`, se limita automáticamente
- Previene errores futuros si el usuario edita config.json manualmente

---

### **4. Validar Valor de Búsquedas Simultáneas al Cargar** (línea 3851)

**Antes:**
```csharp
maxParallelSearches = configManager.GetValue("maxParallelSearches", 3);
```

**Después:**
```csharp
maxParallelSearches = Math.Min(
    configManager.GetValue("maxParallelSearches", 3), 
    30
);
```

**Beneficio:** 
- Si el valor guardado excede 30, se limita automáticamente
- Previene errores al activar modo agresivo

---

## 📈 Comparación

### **Antes (Con Error):**

| Configuración | Valor |
|---------------|-------|
| **maxAlternativeRetries (config)** | 15 |
| **Control Proveedores.Maximum** | 10 |
| **maxParallelSearches (modo agresivo)** | 20 |
| **Control Búsquedas.Maximum** | 15 |
| **Resultado** | ❌ Error al iniciar |

---

### **Después (Corregido):**

| Configuración | Valor |
|---------------|-------|
| **maxAlternativeRetries (config)** | 15 |
| **Control Proveedores.Maximum** | 15 |
| **maxParallelSearches (modo agresivo)** | 20 |
| **Control Búsquedas.Maximum** | 30 |
| **Validación en LoadConfig** | ✅ Sí |
| **Resultado** | ✅ Inicia correctamente |

---

## 🎯 Casos de Uso

### **Caso 1: Valor Normal (≤ 15)**

**Configuración:** `maxAlternativeRetries = 3`

**Resultado:**
- ✅ LoadConfig: `maxAlternativeRetries = 3`
- ✅ Control: `Value = 3` (dentro del rango 0-15)
- ✅ Aplicación inicia correctamente

---

### **Caso 2: Valor Máximo (15)**

**Configuración:** `maxAlternativeRetries = 15`

**Resultado:**
- ✅ LoadConfig: `maxAlternativeRetries = 15`
- ✅ Control: `Value = 15` (dentro del rango 0-15)
- ✅ Aplicación inicia correctamente

---

### **Caso 3: Valor Excesivo (> 15)**

**Configuración:** `maxAlternativeRetries = 20` (editado manualmente)

**Antes (Error):**
- ❌ LoadConfig: `maxAlternativeRetries = 20`
- ❌ Control: `Value = 20` → Error (excede máximo 10)

**Después (Corregido):**
- ✅ LoadConfig: `maxAlternativeRetries = Math.Min(20, 15) = 15`
- ✅ Control: `Value = 15` (dentro del rango 0-15)
- ✅ Aplicación inicia correctamente

---

## 💡 Lógica de Proveedores Alternativos

### **¿Qué son los Proveedores Alternativos?**

Cuando una descarga falla, la aplicación busca **proveedores alternativos** (otros usuarios que tienen el mismo archivo).

**Ejemplo:**
1. Intento descargar de Usuario A → Falla
2. Busco alternativa → Encuentro Usuario B
3. Intento descargar de Usuario B → Falla
4. Busco alternativa → Encuentro Usuario C
5. Intento descargar de Usuario C → Éxito ✅

---

### **Límites:**

| Límite | Valor | Descripción |
|--------|-------|-------------|
| **maxAlternativeRetries** | 0-15 | Número de proveedores alternativos a intentar |
| **MAX_TOTAL_ATTEMPTS** | 15 | Límite absoluto de intentos totales |

**Ejemplo con maxAlternativeRetries = 3:**
- Proveedor original: 3 reintentos
- Alternativa 1: 3 reintentos
- Alternativa 2: 3 reintentos
- Alternativa 3: 3 reintentos
- **Total:** Hasta 12 intentos (o hasta MAX_TOTAL_ATTEMPTS)

---

## ✅ Resultado Final

### **Correcciones:**

1. ✅ **Control Proveedores Alternativos:** Máximo aumentado de 10 a 15
2. ✅ **Control Búsquedas Simultáneas:** Máximo aumentado de 15 a 30
3. ✅ **Validación en LoadConfig:** Ambos valores validados con `Math.Min()`
4. ✅ **Consistencia total:** Controles, configuración y constantes alineados

### **Beneficios:**

- ✅ **Aplicación inicia correctamente** sin errores de validación
- ✅ **Modo Agresivo funciona** (puede establecer 20 búsquedas simultáneas)
- ✅ **Protección contra valores excesivos** en config.json
- ✅ **UI y lógica alineadas** en todos los controles
- ✅ **Valores máximos consistentes** con las necesidades del sistema

---

## 📁 Archivos Modificados

**`MainForm.cs`:**
- Línea 1661: Control Proveedores → Máximo de 10 a `MAX_TOTAL_ATTEMPTS` (15)
- Línea 1696: Control Búsquedas → Máximo de 15 a 30
- Línea 3851: Validación `maxParallelSearches` con `Math.Min(..., 30)`
- Línea 3853: Validación `maxAlternativeRetries` con `Math.Min(..., MAX_TOTAL_ATTEMPTS)`

**`FIX_ERROR_VALOR_15.md`:** Este documento

---

## 🔧 Recomendaciones

### **Para Usuarios:**

1. **No editar config.json manualmente** a menos que sea necesario
2. **Usar la UI** para cambiar configuraciones
3. **Valor recomendado:** 3-5 proveedores alternativos

### **Para Desarrolladores:**

1. **Siempre validar valores** al cargar configuración
2. **Usar constantes** para límites (como `MAX_TOTAL_ATTEMPTS`)
3. **Mantener consistencia** entre UI y lógica

---

**¡El error de valor 15 está corregido!** ✅🔧✨

**Fecha de corrección:** 2025-01-19  
**Versión:** SlskDown v2.0 (Value 15 Error Fixed)
