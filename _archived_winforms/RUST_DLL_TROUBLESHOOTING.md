# 🔧 Rust DLL - Troubleshooting Guide

## 🔴 Problema Actual

La compilación de Rust ejecuta sin errores (`cargo build --release` retorna exit code 0), pero **NO genera** `slskdown_core.dll`.

### Síntomas
- ✅ `cargo build --release` ejecuta sin errores
- ✅ Se generan archivos objeto (`.o`) en `target/release/deps/`
- ❌ NO se genera `slskdown_core.dll` en `target/release/` ni `target/release/deps/`
- ❌ Terminal de Windows CMD/PowerShell no muestra salida de Cargo

### Archivos Generados
```
target/release/deps/
├── slskdown_core.d
└── slskdown_core.slskdown_core.b3d17eb1f64e0291-cgu.0.rcgu.o
```

---

## 🔍 Diagnóstico

### Configuración Verificada ✅
- `Cargo.toml` tiene `crate-type = ["cdylib"]` ✅
- Módulos `search.rs` y `bloom.rs` declarados en `lib.rs` ✅
- Dependencias instaladas correctamente ✅

### Posibles Causas

1. **Errores de enlazado silenciosos**
   - El linker de Windows puede fallar sin mostrar errores en CMD
   - Faltan herramientas de Visual Studio Build Tools

2. **Dependencias de Tantivy incompatibles**
   - Tantivy 0.22 puede tener problemas de enlazado en Windows
   - Conflictos con otras dependencias

3. **Problema con el terminal de Windows**
   - CMD no muestra salida de Cargo (stderr/stdout redirigidos incorrectamente)
   - PowerShell tampoco captura la salida

---

## 🛠️ Soluciones Propuestas

### Opción 1: Compilar con Visual Studio Developer Command Prompt

```cmd
# Abrir "x64 Native Tools Command Prompt for VS 2022"
cd c:\p2p\SlskDown\rust_core
cargo clean
cargo build --release --verbose
```

Esto usa el linker de MSVC correctamente configurado.

---

### Opción 2: Simplificar el Proyecto Rust

Crear una versión mínima sin Tantivy para verificar que la DLL se genera:

**Archivo: `Cargo.toml` (versión simplificada)**
```toml
[package]
name = "slskdown_core"
version = "0.1.0"
edition = "2021"

[lib]
name = "slskdown_core"
crate-type = ["cdylib"]

[dependencies]
probabilistic-collections = "0.7"
dashmap = "6.1"
rayon = "1.8"

[profile.release]
opt-level = 3
lto = true
codegen-units = 1
panic = "abort"
```

**Archivo: `src/lib.rs` (versión simplificada)**
```rust
// Comentar temporalmente el módulo search que usa Tantivy
// pub mod search;
pub mod bloom;

// Exportar solo funciones FFI del bloom filter
pub use bloom::*;
```

Luego compilar:
```cmd
cargo build --release
```

Si esto genera la DLL, el problema es Tantivy. Si no, el problema es más profundo.

---

### Opción 3: Usar Rust sin Tantivy

Implementar búsqueda paralela sin Tantivy usando solo Rayon:

```rust
// src/search_simple.rs
use rayon::prelude::*;

pub fn search_files_parallel(files: &[String], query: &str) -> Vec<String> {
    files.par_iter()
        .filter(|f| f.to_lowercase().contains(&query.to_lowercase()))
        .cloned()
        .collect()
}
```

---

### Opción 4: Verificar Instalación de Rust y MSVC

```cmd
# Verificar Rust
rustc --version
cargo --version

# Verificar target
rustup show

# Instalar target de Windows si falta
rustup target add x86_64-pc-windows-msvc

# Verificar Visual Studio Build Tools
where link.exe
```

---

### Opción 5: Compilar con logging a archivo

```cmd
cd c:\p2p\SlskDown\rust_core
set RUST_BACKTRACE=1
cargo build --release --verbose > build_output.txt 2>&1
type build_output.txt
```

---

## 📋 Checklist de Verificación

- [ ] Visual Studio Build Tools instalado (con componente "Desktop development with C++")
- [ ] Rust instalado con toolchain `x86_64-pc-windows-msvc`
- [ ] Variable de entorno `PATH` incluye Rust y MSVC
- [ ] Compilar desde "Developer Command Prompt" de Visual Studio
- [ ] Probar versión simplificada sin Tantivy
- [ ] Verificar que `link.exe` está disponible

---

## 🔄 Workaround Temporal

**Mientras se resuelve el problema de Rust, la aplicación funciona con:**

✅ **Optimizaciones C# Funcionales (sin Rust):**
1. Structured Logger con Serilog
2. Virtual ListView Cache
3. Object Pooling para DownloadTask
4. Span<T> optimizations

❌ **Optimizaciones que requieren Rust DLL:**
5. Bloom Filter (tiene fallback a HashSet en C#)
6. Búsqueda paralela con Tantivy (usa búsqueda LINQ en C#)

**La aplicación compila y funciona correctamente sin la DLL Rust.**

---

## 📞 Próximos Pasos

1. **Ejecutar desde Visual Studio Developer Command Prompt**
2. **Simplificar Cargo.toml** (quitar Tantivy temporalmente)
3. **Verificar instalación de MSVC Build Tools**
4. **Capturar logs de compilación** en archivo de texto
5. **Considerar alternativa:** Implementar búsqueda en C# con paralelización PLINQ

---

## 📚 Referencias

- [Rust FFI Guide](https://doc.rust-lang.org/nomicon/ffi.html)
- [cdylib on Windows](https://doc.rust-lang.org/reference/linkage.html)
- [Tantivy Windows Issues](https://github.com/quickwit-oss/tantivy/issues)
- [MSVC Linker Troubleshooting](https://rust-lang.github.io/rustup/installation/windows.html)
