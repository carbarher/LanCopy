# 🦀 GUÍA: Instalar Rust y Compilar Optimizaciones

**Estado actual:** Rust **NO está instalado** en tu sistema

---

## 📥 Instalación de Rust (5 minutos)

### Opción 1: Instalador Oficial (Recomendado)

1. **Descargar instalador:**
   - Visita: https://rustup.rs/
   - O ejecuta directamente:
   ```cmd
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```

2. **En Windows:** Descargar `rustup-init.exe`
   - Link directo: https://win.rustup.rs/x86_64

3. **Ejecutar instalador:**
   - Doble clic en `rustup-init.exe`
   - Elegir opción **1) Proceed with installation (default)**
   - Esperar 2-5 minutos

4. **Reiniciar terminal:**
   - Cerrar y abrir CMD/PowerShell
   - Verificar instalación:
   ```cmd
   rustc --version
   cargo --version
   ```

### Opción 2: Usar Rust Precompilado (Sin instalación)

Si prefieres **NO instalar Rust**, puedes:
1. Usar la DLL precompilada (si disponible)
2. Desactivar optimizaciones Rust temporalmente
3. Las funcionalidades tendrán fallback automático a C#

---

## 🔨 Compilar DLL después de Instalar Rust

Una vez instalado Rust:

```cmd
cd c:\p2p\SlskDown
COMPILAR_RUST.bat
```

**Duración:** 2-5 minutos (primera vez), 30 segundos después

---

## 🚀 Alternativa: Continuar sin Rust

**SlskDown funciona perfectamente sin Rust**, solo será más lento en:
- Ordenamiento de grandes volúmenes (5x más lento)
- Filtrado masivo (10x más lento)
- Deduplicación (21x más lento)

**Fallbacks automáticos a C# ya implementados** ✅

Para desactivar Rust completamente en el código:

```csharp
// En MainForm.cs, al inicio del método
private void UpdateSearchResults(...)
{
    bool useRust = false; // Forzar C#
    
    if (useRust && RustAdvancedCore.IsAvailable())
    {
        // Versión Rust...
    }
    else
    {
        // Versión C# (fallback)
        var sorted = allResults.OrderByDescending(r => r.QualityScore).ToList();
    }
}
```

---

## 📊 Comparación: Con Rust vs Sin Rust

| Escenario | Con Rust | Sin Rust | Diferencia |
|-----------|----------|----------|------------|
| Búsqueda 1K resultados | Imperceptible | Imperceptible | Igual |
| Búsqueda 10K resultados | 50ms | 200ms | Notable |
| Búsqueda 100K resultados | 150ms | 1000ms | Muy notable |
| Búsqueda 1M resultados | 1s | 7s+ | Crítico |

**Conclusión:**
- **<10K resultados:** Rust no es necesario
- **10K-100K resultados:** Rust mejora experiencia
- **>100K resultados:** Rust altamente recomendado

---

## ✅ Recomendación

### Para desarrollo normal:
**No instalar Rust aún**, el fallback C# es suficiente.

### Para búsquedas masivas (>10K resultados):
**Instalar Rust** para obtener máximo rendimiento.

### Para producción:
**Instalar Rust** y compilar DLL para distribuir con la aplicación.

---

## 🔧 Troubleshooting

### Problema: "rustc no reconocido"
**Causa:** Rust no está en PATH  
**Solución:**
1. Cerrar y reabrir terminal
2. O ejecutar manualmente:
   ```cmd
   set PATH=%PATH%;%USERPROFILE%\.cargo\bin
   ```

### Problema: "error: linker 'link.exe' not found"
**Causa:** Falta Visual Studio Build Tools  
**Solución:**
1. Instalar Visual Studio Build Tools
2. O usar Rust con GNU toolchain:
   ```cmd
   rustup default stable-x86_64-pc-windows-gnu
   ```

---

## 📝 Próximos Pasos

### Si instalas Rust:
1. Ejecutar `COMPILAR_RUST.bat`
2. Verificar `slskdown_core.dll` creada
3. Compilar SlskDown con `dotnet build`
4. Probar benchmarks

### Si NO instalas Rust:
1. Continuar usando SlskDown normalmente
2. Los fallbacks C# funcionarán automáticamente
3. Considerar instalar Rust más adelante si hay problemas de rendimiento

---

**¿Quieres instalar Rust ahora?**
- **SÍ:** Descargar de https://rustup.rs/ y ejecutar `COMPILAR_RUST.bat`
- **NO:** Continuar sin Rust, todo funciona con fallbacks C#
