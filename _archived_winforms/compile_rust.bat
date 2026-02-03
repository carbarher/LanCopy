@echo off
echo ========================================
echo Compilando Rust Core para SlskDown
echo ========================================
echo.

cd rust_core

echo [1/3] Limpiando compilacion anterior...
cargo clean
echo.

echo [2/3] Compilando en modo Release...
cargo build --release
echo.

if errorlevel 1 (
    echo.
    echo ❌ ERROR: Compilacion fallida
    echo.
    pause
    exit /b 1
)

echo [3/3] Copiando DLL al directorio de salida...
if exist "target\release\slskdown_core.dll" (
    copy /Y "target\release\slskdown_core.dll" "..\bin\Debug\net8.0-windows\" 2>nul
    copy /Y "target\release\slskdown_core.dll" "..\bin\Release\net8.0-windows\" 2>nul
    echo.
    echo ✅ Compilacion exitosa!
    echo.
    echo DLL generada: rust_core\target\release\slskdown_core.dll
    echo.
) else (
    echo.
    echo ❌ ERROR: DLL no encontrada
    echo.
    pause
    exit /b 1
)

cd ..

echo ========================================
echo Proceso completado
echo ========================================
echo.
pause
