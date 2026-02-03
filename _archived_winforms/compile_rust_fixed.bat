@echo off
echo ========================================
echo Compilando Rust Core para SlskDown
echo ========================================
echo.

REM Matar procesos que puedan estar bloqueando archivos
echo [0/4] Matando procesos bloqueantes...
taskkill /F /IM rust-analyzer.exe 2>nul
taskkill /F /IM rls.exe 2>nul
timeout /t 2 /nobreak >nul
echo.

cd rust_core

echo [1/4] Limpiando compilacion anterior...
cargo clean
echo.

echo [2/4] Esperando liberacion de archivos...
timeout /t 3 /nobreak >nul
echo.

echo [3/4] Compilando en modo Release...
cargo build --release
echo.

if errorlevel 1 (
    echo.
    echo ❌ ERROR: Compilacion fallida
    echo.
    echo Intentando compilacion incremental...
    cargo build --release
    if errorlevel 1 (
        echo.
        echo ❌ ERROR: Compilacion fallida definitivamente
        echo.
        cd ..
        pause
        exit /b 1
    )
)

echo [4/4] Copiando DLL al directorio de salida...
if exist "target\release\slskdown_core.dll" (
    if not exist "..\bin\Debug\net8.0-windows\" mkdir "..\bin\Debug\net8.0-windows\"
    if not exist "..\bin\Release\net8.0-windows\" mkdir "..\bin\Release\net8.0-windows\"
    
    copy /Y "target\release\slskdown_core.dll" "..\bin\Debug\net8.0-windows\" 2>nul
    copy /Y "target\release\slskdown_core.dll" "..\bin\Release\net8.0-windows\" 2>nul
    copy /Y "target\release\slskdown_core.dll" "..\" 2>nul
    echo.
    echo ✅ Compilacion exitosa!
    echo.
    echo DLL generada: rust_core\target\release\slskdown_core.dll
    echo DLL copiada a: bin\Debug y bin\Release
    echo.
) else (
    echo.
    echo ❌ ERROR: DLL no encontrada
    echo.
    cd ..
    pause
    exit /b 1
)

cd ..

echo ========================================
echo Proceso completado
echo ========================================
echo.
pause
