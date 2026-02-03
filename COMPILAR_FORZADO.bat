@echo off
setlocal enabledelayedexpansion

echo ========================================
echo COMPILACION FORZADA - SlskDown
echo ========================================
echo.

REM Matar procesos que puedan estar usando los archivos
echo Cerrando procesos existentes...
taskkill /F /IM SlskDown.exe 2>nul
taskkill /F /IM TestCreds.exe 2>nul
timeout /t 2 /nobreak >nul
echo OK - Procesos cerrados
echo.

REM Cambiar al directorio del script
cd /d "%~dp0"

REM Verificar que existe la carpeta SlskDown
if not exist "SlskDown" (
    echo ERROR: No se encuentra la carpeta SlskDown
    pause
    exit /b 1
)

cd SlskDown

REM Verificar que existe el archivo .csproj
if not exist "SlskDown.csproj" (
    echo ERROR: No se encuentra SlskDown.csproj
    pause
    exit /b 1
)

echo [1/3] Compilando Rust (slsk_native)...
if exist "slsk_native\Cargo.toml" (
    cd slsk_native
    cargo build --release
    if !errorlevel! neq 0 (
        echo ERROR: Fallo en compilacion de Rust
        cd ..
        cd ..
        pause
        exit /b 1
    )
    echo OK - Rust compilado exitosamente
    cd ..
) else (
    echo AVISO: No se encuentra slsk_native, omitiendo compilacion Rust
)
echo.

echo [2/3] Limpiando proyecto C#...
dotnet clean SlskDown.csproj
if %errorlevel% neq 0 (
    echo ERROR: Fallo en limpieza
    cd ..
    pause
    exit /b 1
)
echo OK - Proyecto limpiado
echo.

echo [3/3] Compilando proyecto C#...
dotnet build SlskDown.csproj -c Release --verbosity normal
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo ERROR: Fallo en compilacion C#
    echo ========================================
    cd ..
    pause
    exit /b 1
)

echo.
echo ========================================
echo COMPILACION EXITOSA
echo ========================================
echo.
echo Rust: OK (slsk_native.dll)
echo C#:   OK (SlskDown.exe)
echo.
echo Archivos generados en: bin\Release\net8.0-windows\
echo.

cd ..
pause
