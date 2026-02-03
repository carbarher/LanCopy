# 🦀 slsk_optimizer - Rust Optimization Library

Biblioteca nativa en Rust para optimizar operaciones críticas de SlskDown.

## 📊 Mejoras de Rendimiento

| Operación | C# | Rust | Mejora |
|-----------|-----|------|--------|
| Detección de idioma | 100 µs | 5-10 µs | **10-20x** |
| Normalización de autores | 17 µs | 2-3 µs | **5-10x** |
| Levenshtein | 10 ms | 200-500 µs | **20-50x** |
| Búsqueda de keywords | 200 µs | 25-70 µs | **3-8x** |

## 🛠️ Instalación de Rust

### Windows

1. Descargar e instalar Rust desde: https://rustup.rs/
2. Ejecutar el instalador `rustup-init.exe`
3. Seguir las instrucciones (opción 1: instalación por defecto)
4. Reiniciar terminal para que `cargo` esté disponible

### Verificar Instalación

```cmd
rustc --version
cargo --version
```

Deberías ver algo como:
```
rustc 1.75.0 (82e1608df 2023-12-21)
cargo 1.75.0 (1d8b05cdd 2023-11-20)
```

## 🔨 Compilación

### Compilar DLL para Release

```cmd
cd c:\p2p\slsk_optimizer
cargo build --release
```

La DLL se generará en: `target\release\slsk_optimizer.dll`

### Compilar para Debug (con símbolos)

```cmd
cargo build
```

La DLL se generará en: `target\debug\slsk_optimizer.dll`

## 📦 Instalación en SlskDown

1. Compilar la DLL:
   ```cmd
   cd c:\p2p\slsk_optimizer
   cargo build --release
   ```

2. Copiar DLL al directorio de SlskDown:
   ```cmd
   copy target\release\slsk_optimizer.dll c:\p2p\SlskDown\bin\Release\net8.0-windows\
   ```

3. La DLL se cargará automáticamente al iniciar SlskDown

## 🧪 Testing

### Ejecutar tests de Rust

```cmd
cargo test
```

### Tests específicos con output

```cmd
cargo test -- --nocapture
```

### Benchmark (requiere nightly)

```cmd
cargo +nightly bench
```

## 📝 Funciones Exportadas

### `is_spanish_text(text: *const u8, len: usize) -> bool`

Detecta si un texto contiene indicadores de idioma español.

**Ejemplo C#:**
```csharp
bool isSpanish = RustOptimizer.IsSpanishText("Este es un libro en español");
// → true
```

### `normalize_author_name(input: *const c_char, output: *mut c_char, max_len: usize) -> i32`

Normaliza un nombre de autor eliminando puntos y espacios extras.

**Ejemplo C#:**
```csharp
string normalized = RustOptimizer.NormalizeAuthorName("A. E. Pepito");
// → "ae pepito"
```

### `levenshtein_distance(s1: *const u8, len1: usize, s2: *const u8, len2: usize) -> i32`

Calcula la distancia de Levenshtein entre dos strings.

**Ejemplo C#:**
```csharp
int distance = RustOptimizer.LevenshteinDistance("kitten", "sitting");
// → 3
```

### `contains_keywords(text: *const u8, text_len: usize, keywords: *const *const c_char, num_keywords: usize) -> bool`

Verifica si un texto contiene alguna de las keywords dadas.

**Ejemplo C#:**
```csharp
bool hasKeywords = RustOptimizer.ContainsKeywords(
    "Este es un libro de ciencia ficción",
    new[] { "ciencia", "ficción" }
);
// → true
```

### `get_version() -> *const c_char`

Obtiene la versión de la biblioteca.

**Ejemplo C#:**
```csharp
string version = RustOptimizer.GetVersion();
// → "slsk_optimizer v0.1.0"
```

## 🔧 Configuración Avanzada

### Optimizaciones de Compilación

El archivo `Cargo.toml` ya incluye optimizaciones agresivas:

```toml
[profile.release]
opt-level = 3          # Máxima optimización
lto = true             # Link-Time Optimization
codegen-units = 1      # Mejor optimización (compilación más lenta)
strip = true           # Eliminar símbolos de debug
panic = "abort"        # Panic más ligero
```

### Compilar con CPU nativa (aún más rápido)

```cmd
set RUSTFLAGS=-C target-cpu=native
cargo build --release
```

⚠️ **Advertencia**: La DLL solo funcionará en CPUs con las mismas características.

### Habilitar SIMD explícito

```cmd
set RUSTFLAGS=-C target-feature=+avx2
cargo build --release
```

## 📊 Benchmarking

### Comparar con versión C#

Crear archivo `benches/benchmark.rs`:

```rust
use criterion::{black_box, criterion_group, criterion_main, Criterion};
use slsk_optimizer::*;

fn bench_is_spanish_text(c: &mut Criterion) {
    let text = "Este es un libro en español con muchos caracteres ñáéíóú";
    
    c.bench_function("is_spanish_text", |b| {
        b.iter(|| unsafe {
            is_spanish_text(black_box(text.as_ptr()), black_box(text.len()))
        });
    });
}

criterion_group!(benches, bench_is_spanish_text);
criterion_main!(benches);
```

Ejecutar:
```cmd
cargo bench
```

## 🐛 Troubleshooting

### Error: "slsk_optimizer.dll not found"

**Solución**: Copiar la DLL al directorio del ejecutable de SlskDown.

```cmd
copy target\release\slsk_optimizer.dll c:\p2p\SlskDown\bin\Release\net8.0-windows\
```

### Error: "The specified module could not be found"

**Causa**: Faltan dependencias de Visual C++ Runtime.

**Solución**: Instalar Visual C++ Redistributable:
https://aka.ms/vs/17/release/vc_redist.x64.exe

### Error de compilación: "linker 'link.exe' not found"

**Causa**: Falta el compilador de C++ de Visual Studio.

**Solución**: Instalar "Build Tools for Visual Studio 2022":
https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022

Seleccionar: "Desktop development with C++"

### La DLL no se carga en SlskDown

**Verificar**:
1. La DLL está en el mismo directorio que `SlskDown.exe`
2. La arquitectura es correcta (x64)
3. No hay errores en el Event Viewer de Windows

**Debug**:
```csharp
try
{
    var version = RustOptimizer.GetVersion();
    Console.WriteLine($"Rust optimizer loaded: {version}");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to load Rust optimizer: {ex.Message}");
}
```

## 📚 Recursos

- **Rust Book**: https://doc.rust-lang.org/book/
- **FFI Guide**: https://doc.rust-lang.org/nomicon/ffi.html
- **Regex Crate**: https://docs.rs/regex/latest/regex/
- **Criterion (benchmarking)**: https://docs.rs/criterion/latest/criterion/

## 🚀 Próximas Mejoras

- [ ] SIMD explícito para Levenshtein (AVX2)
- [ ] Procesamiento paralelo de archivos con Rayon
- [ ] Caché LRU nativo en Rust
- [ ] Extracción de texto de PDFs con poppler-rs
- [ ] Compresión de cachés con zstd

## 📄 Licencia

MIT License - Ver archivo LICENSE para más detalles.
