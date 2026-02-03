# Presets de Configuración y Sistema de Búsqueda

## 📋 Resumen

Se han implementado dos nuevas características en el tab de Configuración de SlskDown:

1. **Sistema de Presets** - Aplicar configuraciones predefinidas con un clic
2. **Búsqueda en Tiempo Real** - Filtrar opciones escribiendo palabras clave

---

## 🎯 1. Sistema de Presets

### Descripción

Tres botones que aplican configuraciones predefinidas optimizadas para diferentes escenarios de uso.

### Presets Disponibles

#### 🐢 **Conservador**
Configuración segura para conexiones lentas o inestables.

**Valores aplicados:**
- Descargas simultáneas: **3**
- Búsquedas paralelas: **3**
- Timeout: **30 segundos**
- Reintentos: **2**
- Proveedores alternativos: **2**
- Límite respuestas: **50**
- Límite archivos: **100**
- Modo Turbo: **Desactivado**

**Ideal para:**
- Conexiones lentas (< 5 Mbps)
- Redes inestables
- Usuarios con límites de ancho de banda
- Primeras pruebas de la aplicación

---

#### ⚖️ **Balanceado** (Recomendado)
Equilibrio entre velocidad y estabilidad.

**Valores aplicados:**
- Descargas simultáneas: **5**
- Búsquedas paralelas: **6**
- Timeout: **25 segundos**
- Reintentos: **3**
- Proveedores alternativos: **3**
- Límite respuestas: **100**
- Límite archivos: **200**
- Modo Turbo: **Desactivado**

**Ideal para:**
- Conexiones medias (5-20 Mbps)
- Mayoría de usuarios
- Uso diario normal
- Balance entre velocidad y recursos

---

#### 🚀 **Agresivo**
Máxima velocidad para conexiones rápidas.

**Valores aplicados:**
- Descargas simultáneas: **8**
- Búsquedas paralelas: **12**
- Timeout: **20 segundos**
- Reintentos: **3**
- Proveedores alternativos: **3**
- Límite respuestas: **200**
- Límite archivos: **500**
- Modo Turbo: **Activado**

**Ideal para:**
- Conexiones rápidas (> 20 Mbps)
- Redes estables
- Usuarios avanzados
- Máximo rendimiento

---

### Uso

1. Abrir tab **Configuración**
2. Localizar botones de presets debajo de la barra de búsqueda
3. Clic en el preset deseado
4. Ver badge temporal con confirmación
5. Configuración se guarda automáticamente

### Feedback Visual

Al aplicar un preset:
- Badge temporal aparece durante 3 segundos
- Log muestra valores aplicados
- Controles UI se actualizan automáticamente

**Ejemplo de log:**
```
✅ Preset BALANCEADO aplicado:
  • 5 descargas simultáneas
  • 6 búsquedas paralelas
  • Timeout: 25 segundos
  • 3 reintentos por archivo
  • Recomendado para la mayoría de usuarios
```

---

## 🔍 2. Sistema de Búsqueda en Tiempo Real

### Descripción

Barra de búsqueda que filtra paneles de configuración en tiempo real según palabras clave.

### Características

**Búsqueda inteligente:**
- Busca en títulos de paneles
- Busca en palabras clave asociadas
- Auto-expande paneles con coincidencias
- Oculta paneles sin coincidencias

**Keywords por panel:**

| Panel | Keywords |
|-------|----------|
| 🔐 Cuenta | usuario, contraseña, password, carpeta, descargas, directorio |
| ⚡ Opciones | auto, conectar, organizar, autor, backup, tamaño, bytes |
| 📥 Descargas | turbo, descarga, simultánea, paralela, reintento, retry, proveedor, alternativa, mínimo, kb |
| 🌐 Red | timeout, respuesta, archivo, búsqueda, simultánea, puerto, listen, red, distribuida |
| 🚀 Nicotine+ | reconexión, retry, batch, pequeño, prioridad, slot, continua |
| 🎨 Interfaz | notificación, sonido, interfaz, ui |
| 🤖 IA | ia, inteligencia, artificial, ollama, asistente, chat |

### Ejemplos de Uso

**Buscar "turbo":**
- Expande panel **📥 DESCARGAS**
- Oculta todos los demás paneles
- Muestra opción "Modo Turbo"

**Buscar "puerto":**
- Expande panel **🌐 RED Y BÚSQUEDA**
- Muestra control "Puerto de escucha"

**Buscar "usuario":**
- Expande panel **🔐 CUENTA**
- Muestra campo "Usuario Soulseek"

**Buscar "sonido":**
- Expande panel **🎨 INTERFAZ**
- Muestra checkbox "Sonidos de notificación"

### Comportamiento

**Texto vacío o placeholder:**
- Muestra todos los paneles
- Estado normal

**Texto con coincidencias:**
- Auto-expande paneles relevantes
- Oculta paneles sin coincidencias
- Mantiene scroll en posición

**Sin coincidencias:**
- Muestra mensaje en log
- Todos los paneles ocultos
- Sugerencia de refinar búsqueda

### Placeholder

**Texto:** "Buscar configuración..."  
**Color:** Gris claro  
**Comportamiento:**
- Desaparece al hacer foco
- Reaparece si campo queda vacío

---

## 🎨 Interfaz

### Layout del Header

```
⚙️ Configuración

🔍 [Buscar configuración...        ]

Presets:  [🐢 Conservador]  [⚖️ Balanceado]  [🚀 Agresivo]

─────────────────────────────────────────────────────────
[Paneles colapsables aquí]
```

### Dimensiones

- **Header total:** 140px altura
- **Título:** 20pt, bold
- **Barra búsqueda:** 300px ancho, 28px alto
- **Botones preset:** 140px ancho, 35px alto
- **Espaciado:** 8px entre elementos

---

## 💻 Implementación Técnica

### Variables de Clase

```csharp
private TextBox txtConfigSearch;
private List<CollapsiblePanel> allConfigPanels = new List<CollapsiblePanel>();
private Dictionary<string, List<Control>> panelSearchableControls = new Dictionary<string, List<Control>>();
```

### Métodos Principales

#### `FilterConfigPanels()`
Filtra paneles según texto de búsqueda.

**Lógica:**
1. Obtener texto de búsqueda (lowercase)
2. Si vacío → mostrar todos
3. Buscar en títulos de paneles
4. Buscar en keywords asociadas
5. Ocultar/mostrar paneles según coincidencias
6. Auto-expandir paneles con coincidencias

#### `ApplyPreset(string preset)`
Aplica configuración predefinida.

**Parámetros:**
- `"conservador"` - Preset conservador
- `"balanceado"` - Preset balanceado
- `"agresivo"` - Preset agresivo

**Acciones:**
1. Actualizar variables de configuración
2. Actualizar controles UI (checkboxes, numerics)
3. Mostrar log con valores aplicados
4. Mostrar badge temporal
5. Guardar configuración

---

## 🧪 Testing

### Casos de Prueba - Presets

✅ **Aplicar Conservador:**
- Valores correctos aplicados
- Badge "🐢 Modo Conservador" aparece
- Log muestra configuración
- Modo Turbo desactivado

✅ **Aplicar Balanceado:**
- Valores intermedios aplicados
- Badge "⚖️ Modo Balanceado" aparece
- Configuración guardada

✅ **Aplicar Agresivo:**
- Valores máximos aplicados
- Badge "🚀 Modo Agresivo" aparece
- Modo Turbo activado

### Casos de Prueba - Búsqueda

✅ **Búsqueda con coincidencias:**
- Paneles correctos expandidos
- Paneles sin coincidencias ocultos
- Scroll funciona correctamente

✅ **Búsqueda sin coincidencias:**
- Todos los paneles ocultos
- Mensaje en log
- No hay errores

✅ **Limpiar búsqueda:**
- Todos los paneles visibles
- Estado normal restaurado

✅ **Placeholder:**
- Aparece/desaparece correctamente
- Color gris cuando inactivo
- Color blanco cuando activo

---

## 📊 Comparativa de Presets

| Característica | Conservador | Balanceado | Agresivo |
|----------------|-------------|------------|----------|
| **Descargas** | 3 | 5 | 8 |
| **Búsquedas** | 3 | 6 | 12 |
| **Timeout** | 30s | 25s | 20s |
| **Reintentos** | 2 | 3 | 3 |
| **Alternativos** | 2 | 3 | 3 |
| **Respuestas** | 50 | 100 | 200 |
| **Archivos** | 100 | 200 | 500 |
| **Modo Turbo** | ❌ | ❌ | ✅ |
| **Velocidad** | 🐢 Lenta | ⚖️ Media | 🚀 Rápida |
| **Estabilidad** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Uso CPU** | Bajo | Medio | Alto |
| **Uso Red** | Bajo | Medio | Alto |

---

## 🎯 Ventajas

### Presets

✅ **Facilidad de uso** - Un clic para configurar  
✅ **Optimización automática** - Valores probados y balanceados  
✅ **Aprendizaje** - Usuarios ven qué valores funcionan  
✅ **Reversible** - Fácil cambiar entre presets  
✅ **Feedback claro** - Badges y logs informativos  

### Búsqueda

✅ **Navegación rápida** - Encontrar opciones en segundos  
✅ **Descubrimiento** - Usuarios encuentran opciones desconocidas  
✅ **Productividad** - Menos scroll, más eficiencia  
✅ **Intuitivo** - Funciona como búsquedas web  
✅ **Auto-expansión** - Paneles se abren automáticamente  

---

## 🚀 Mejoras Futuras (Opcional)

### Presets

1. **Preset Personalizado**
   - Botón "Guardar como preset"
   - Nombre personalizado
   - Cargar presets guardados

2. **Importar/Exportar**
   - Compartir presets con otros usuarios
   - Formato JSON
   - Biblioteca de presets comunitarios

3. **Preset por Horario**
   - Conservador durante el día
   - Agresivo durante la noche
   - Programación automática

### Búsqueda

1. **Búsqueda Avanzada**
   - Operadores booleanos (AND, OR, NOT)
   - Búsqueda por categoría
   - Historial de búsquedas

2. **Resaltado de Coincidencias**
   - Highlight en controles que coinciden
   - Scroll automático al primer resultado
   - Navegación entre resultados (F3)

3. **Sugerencias**
   - Autocompletar mientras escribe
   - Búsquedas populares
   - Corrección de typos

---

## 📝 Código Relevante

### Ubicación

**Archivo:** `MainForm.ConfigTab.cs`

**Líneas:**
- Variables: 12-14
- Header con búsqueda: 36-139
- Evento búsqueda: 163
- Método `FilterConfigPanels()`: 813-892
- Método `ApplyPreset()`: 897-993

### Dependencias

- `CollapsiblePanel.cs` - Paneles colapsables
- `VisualFeedbackHelper.cs` - Badges temporales
- `MainForm.cs` - Variables de configuración

---

## ✅ Estado

- **Implementación:** ✅ Completa
- **Compilación:** ✅ Sin errores
- **Testing:** ✅ Funcional
- **Documentación:** ✅ Completa

---

## 🎓 Conclusión

Las nuevas características de **Presets** y **Búsqueda** mejoran significativamente la usabilidad del tab de Configuración:

- **Presets:** Configuración rápida para diferentes escenarios
- **Búsqueda:** Navegación instantánea a cualquier opción
- **Feedback:** Badges y logs informativos
- **Productividad:** Menos tiempo configurando, más tiempo descargando

Estas mejoras complementan perfectamente el sistema de paneles colapsables implementado anteriormente.

---

**Fecha:** 13 de enero de 2026  
**Versión:** 1.0  
**Estado:** ✅ Completado  
**Autor:** Cascade AI Assistant
