@echo off
echo ========================================
echo   COMPILACION COMPLETA - SlskDown
echo ========================================
echo.

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%" >nul

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

if /I not "%CONFIG%"=="Debug" if /I not "%CONFIG%"=="Release" (
    echo.
    echo ERROR: Config invalida: %CONFIG%
    echo Usa: build_all.bat [Debug^|Release]
    echo.
    popd >nul
    exit /b 1
)

echo [1/3] Compilando Rust...
pushd rust_core >nul
cargo build --release
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Fallo la compilacion de Rust
    popd >nul
    pause
    exit /b 1
)
popd >nul
echo    OK: Rust compilado exitosamente
echo.

echo [2/3] Compilando C# (%CONFIG%)...
dotnet build SlskDown.csproj -c %CONFIG%
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Fallo la compilacion de C#
    pause
    exit /b 1
)
echo.

echo [3/3] Verificando DLL Rust en output...
set "OUT_DIR=bin\%CONFIG%\net8.0-windows"
if exist "%OUT_DIR%\slskdown_core.dll" (
    echo    OK: slskdown_core.dll presente en %OUT_DIR%
) else (
    echo.
    echo WARNING: slskdown_core.dll NO esta en %OUT_DIR%
    echo          Revisa que exista rust_core\target\release\slskdown_core.dll
)

echo.

echo ========================================
echo   COMPILACION EXITOSA
echo ========================================
echo.
echo Para ejecutar: dotnet run -c %CONFIG%
echo.
pause

popd >nul
endlocal
