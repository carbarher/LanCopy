@echo off
echo ========================================
echo   COMPILANDO MODULO RUST AVANZADO
echo ========================================
echo.

cd rust_core

echo [1/3] Limpiando compilacion anterior...
cargo clean
echo.

echo [2/3] Compilando con optimizaciones maximas...
cargo build --release
if errorlevel 1 (
    echo.
    echo ❌ ERROR: Compilacion fallida
    pause
    exit /b 1
)
echo.

echo [3/3] Copiando DLL al directorio principal...
copy /Y target\release\slskdown_core.dll ..\slskdown_core.dll
if errorlevel 1 (
    echo.
    echo ❌ ERROR: No se pudo copiar la DLL
    pause
    exit /b 1
)

cd ..

echo.
echo ========================================
echo   ✅ COMPILACION EXITOSA
echo ========================================
echo.
echo DLL ubicada en: %CD%\slskdown_core.dll
echo.
echo Proximo paso:
echo   1. Compilar SlskDown.csproj
echo   2. Probar funcionalidades Rust
echo.

pause
