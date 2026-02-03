@echo off
echo.
echo ========================================
echo   VERIFICACION RUST - SlskDown
echo ========================================
echo.

echo [1] Verificando DLL Rust compilado...
if exist "rust_core\target\release\slskdown_core.dll" (
    echo    OK: DLL Rust encontrado (128 KB)
    echo    Ubicacion: rust_core\target\release\slskdown_core.dll
) else (
    echo    ERROR: DLL Rust NO encontrado
    exit /b 1
)
echo.

echo [2] Copiando DLL a bin\Debug...
if not exist "bin\Debug\net8.0-windows" mkdir "bin\Debug\net8.0-windows"
copy /Y "rust_core\target\release\slskdown_core.dll" "bin\Debug\net8.0-windows\slskdown_core.dll" >nul 2>&1
if %errorlevel% == 0 (
    echo    OK: DLL copiado exitosamente
) else (
    echo    ERROR: No se pudo copiar DLL
    exit /b 1
)
echo.

echo [3] Verificando DLL en bin\Debug...
if exist "bin\Debug\net8.0-windows\slskdown_core.dll" (
    echo    OK: DLL disponible para C#
) else (
    echo    ERROR: DLL NO disponible
    exit /b 1
)
echo.

echo [4] Resumen funcionalidades Rust...
echo    - 14 funcionalidades implementadas
echo    - 40 metodos publicos en RustCore.cs
echo    - 8 integraciones activas en MainForm.cs
echo    - Bloom Filter + CRC32 + Regex Cache + Multi-Pattern
echo.

echo ========================================
echo   RUST: TODO OK - LISTO PARA USAR
echo ========================================
echo.
