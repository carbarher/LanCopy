@echo off
echo ========================================
echo  Building slsk_optimizer (Rust)
echo ========================================
echo.

REM Verificar que Rust está instalado
where cargo >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Rust/Cargo no encontrado
    echo.
    echo Por favor instala Rust desde: https://rustup.rs/
    echo.
    pause
    exit /b 1
)

echo [1/4] Verificando version de Rust...
cargo --version
rustc --version
echo.

echo [2/4] Compilando en modo Release (optimizado)...
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Compilacion fallida
    pause
    exit /b 1
)
echo.

echo [3/4] Ejecutando tests...
cargo test --release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ADVERTENCIA: Algunos tests fallaron
    echo Continuar de todas formas? (S/N)
    set /p continue=
    if /i not "%continue%"=="S" exit /b 1
)
echo.

echo [4/4] Copiando DLL a SlskDown...

REM Buscar directorio de SlskDown
set SLSK_DIR=..\SlskDown\bin\Release\net8.0-windows

if not exist "%SLSK_DIR%" (
    echo Creando directorio: %SLSK_DIR%
    mkdir "%SLSK_DIR%"
)

copy /Y target\release\slsk_optimizer.dll "%SLSK_DIR%\"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: No se pudo copiar la DLL
    pause
    exit /b 1
)

echo.
echo ========================================
echo  BUILD EXITOSO!
echo ========================================
echo.
echo DLL ubicada en:
echo   target\release\slsk_optimizer.dll
echo.
echo DLL copiada a:
echo   %SLSK_DIR%\slsk_optimizer.dll
echo.
echo Tamaño de la DLL:
dir target\release\slsk_optimizer.dll | find "slsk_optimizer.dll"
echo.
echo Para usar en SlskDown:
echo   1. Compilar SlskDown (dotnet build)
echo   2. Ejecutar SlskDown.exe
echo   3. Verificar log: "Rust optimizer loaded: v0.1.0"
echo.
pause
