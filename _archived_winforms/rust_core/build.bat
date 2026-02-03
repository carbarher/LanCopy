@echo off
echo ========================================
echo    Building SlskDown Rust Core
echo ========================================
echo.

REM Verificar que Rust está instalado
where cargo >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Rust no está instalado!
    echo.
    echo Instalar desde: https://rustup.rs/
    echo.
    pause
    exit /b 1
)

echo [INFO] Rust encontrado: 
cargo --version
echo.

REM Compilar en modo release
echo [INFO] Compilando en modo release...
cargo build --release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Error al compilar Rust core
    pause
    exit /b 1
)

echo.
echo ========================================
echo    Build completado exitosamente!
echo ========================================
echo.
echo DLL generada en:
echo   rust_core\target\release\slskdown_core.dll
echo.

REM Verificar tamaño del archivo
if exist target\release\slskdown_core.dll (
    for %%I in (target\release\slskdown_core.dll) do (
        echo Tamaño: %%~zI bytes
    )
)

echo.
pause
