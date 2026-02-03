# ⚠️ ACCIÓN REQUERIDA: Editar lib.rs Manualmente

## Problema
El archivo `c:\p2p\SlskDown\rust_core\src\lib.rs` tiene ~1800 líneas de código antiguo con dependencias que ya no están en `Cargo.toml`, causando **268 errores de compilación**.

## Solución: Editar Manualmente

### Paso 1: Abrir el archivo
```
Abrir: c:\p2p\SlskDown\rust_core\src\lib.rs
```

### Paso 2: Seleccionar TODO el contenido
```
Ctrl+A (seleccionar todo)
```

### Paso 3: Reemplazar con estas 3 líneas
```rust
// Módulos de optimización Rust para SlskDown
pub mod bloom;   // Bloom filter para deduplicación
pub mod search;  // Motor de búsqueda full-text con Tantivy
```

### Paso 4: Guardar
```
Ctrl+S
```

### Paso 5: Compilar
```powershell
cd c:\p2p\SlskDown\rust_core
cargo build --release
```

---

## Alternativa: Usar PowerShell ISE

Si el editor no funciona, abre PowerShell ISE:

```powershell
# Abrir PowerShell ISE
powershell_ise.exe

# Ejecutar esto en la consola:
@"
// Módulos de optimización Rust para SlskDown
pub mod bloom;   // Bloom filter para deduplicación
pub mod search;  // Motor de búsqueda full-text con Tantivy
"@ | Out-File -FilePath "c:\p2p\SlskDown\rust_core\src\lib.rs" -Encoding UTF8 -Force
```

---

## Verificar que funcionó

```powershell
# Ver contenido del archivo (debe mostrar solo 3 líneas)
Get-Content "c:\p2p\SlskDown\rust_core\src\lib.rs"
```

---

**Una vez editado, avísame para compilar Rust y generar la DLL.**
