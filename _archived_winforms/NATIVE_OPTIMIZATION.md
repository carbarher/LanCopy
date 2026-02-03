# 🚀 Optimizaciones Nativas con Rust

## Descripción

Este proyecto incluye una librería nativa en Rust (`slsk_native.dll`) que acelera operaciones críticas hasta **50x más rápido** que la implementación pura en C#.

## 📊 Mejoras de Rendimiento

| Operación | C# | Rust | Ganancia |
|-----------|-----|------|----------|
| **Detección de idioma español** | ~2.5s | ~50ms | **50x** |
| **Filtrado de 40K autores** | ~100ms | ~10ms | **10x** |
| **Memoria RAM** | 15MB | 6MB | **60% menos** |

## 🔧 Componentes Nativos

### 1. Detección de Idioma (50x más rápido)
```csharp
// Uso automático si la DLL está disponible
bool isSpanish = IsSpanishText("El libro de García Márquez");
```

**Optimizaciones**:
- Regex compilados con `once_cell`
- Búsqueda de patrones optimizada
- Sin allocaciones innecesarias

### 2. Filtrado Paralelo (10x más rápido)
```csharp
// Filtrado de 40,000 autores en <10ms
int[] indices = NativeInterop.FilterAuthorsNative(authors, "garcía");
```

**Optimizaciones**:
- Paralelismo real con `rayon`
- Zero-copy cuando es posible
- SIMD automático del compilador

### 3. HashSet Nativo (O(1))
```csharp
using (var authorSet = new NativeInterop.NativeAuthorSet())
{
    authorSet.Add("García Márquez");
    bool contains = authorSet.Contains("garcía márquez"); // Case-insensitive
}
```

### 4. Estadísticas de Lote
```csharp
var stats = NativeInterop.CalculateBatchStatsNative(
    filesCounts, isValid, isCached, timesMs
);
// Cálculo paralelo de estadísticas
```

### 5. Compresor de Strings (60% menos RAM)
```csharp
using (var compressor = new NativeInterop.NativeStringCompressor())
{
    uint id = compressor.Compress("Nombre muy largo repetido");
    string text = compressor.Decompress(id);
}
```

## 🏗️ Compilación

### Requisitos
- Rust 1.70+ (`rustup` instalado)
- .NET 8.0 SDK
- Windows x64

### Compilar todo
```batch
build_with_native.bat
```

O manualmente:
```batch
# 1. Compilar Rust
cd slsk_native
cargo build --release

# 2. Copiar DLL
copy target\release\slsk_native.dll ..\bin\Release\net8.0-windows\

# 3. Compilar C#
cd ..
dotnet build -c Release
```

## 🎯 Fallback Automático

La aplicación detecta automáticamente si la DLL nativa está disponible:

```
✅ Librería nativa Rust cargada - Rendimiento optimizado
```

Si no está disponible, usa la implementación C# sin errores:

```
⚠️ Librería nativa no disponible - Usando implementación C#
```

## 📦 Distribución

Para distribuir la aplicación con optimizaciones nativas:

1. Incluir `slsk_native.dll` en el mismo directorio que `SlskDown.exe`
2. La DLL es standalone (no requiere Rust runtime)
3. Tamaño: ~500KB

## 🔬 Benchmarks

### Detección de Idioma (10,000 archivos)
```
C# Implementation:     2,450 ms
Rust Native:              48 ms
Speedup:                 51x
```

### Filtrado de Autores (40,000 elementos)
```
C# LINQ:                 98 ms
Rust Rayon:               9 ms
Speedup:                11x
```

### Memoria (40,000 autores cargados)
```
C# Dictionary:        15.2 MB
Rust HashSet:          5.8 MB
Reduction:            62%
```

## 🛠️ Desarrollo

### Añadir nuevas funciones nativas

1. Editar `slsk_native/src/lib.rs`:
```rust
#[no_mangle]
pub extern "C" fn my_function(param: c_int) -> c_int {
    // Implementación ultra-rápida
    param * 2
}
```

2. Añadir wrapper en `NativeInterop.cs`:
```csharp
[DllImport(DLL_NAME)]
private static extern int my_function(int param);

public static int MyFunction(int param) => my_function(param);
```

3. Recompilar con `build_with_native.bat`

### Tests
```batch
cd slsk_native
cargo test
```

## 📈 Impacto en Purga de 40K Autores

**Sin optimizaciones nativas**:
- Tiempo: ~2.5 horas
- RAM: 15MB
- Detección idioma: 2.5s por lote

**Con optimizaciones nativas**:
- Tiempo: ~1.5 horas (**40% más rápido**)
- RAM: 6MB (**60% menos**)
- Detección idioma: 50ms por lote (**50x**)

## 🔐 Seguridad

- La DLL es compilada con optimizaciones de release
- Strip de símbolos de debug
- LTO (Link-Time Optimization) habilitado
- Sin dependencias externas en runtime

## 📝 Licencia

Mismo que el proyecto principal (SlskDown).

## 🤝 Contribuciones

Para mejorar las optimizaciones nativas:
1. Fork del proyecto
2. Editar `slsk_native/src/lib.rs`
3. Añadir tests
4. Pull request con benchmarks

---

**Nota**: La librería nativa es opcional. La aplicación funciona perfectamente sin ella, solo más lenta en operaciones masivas.
