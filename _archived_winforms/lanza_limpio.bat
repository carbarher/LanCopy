@echo off
echo ========================================
echo COMPILAR Y EJECUTAR SLSKDOWN (LIMPIO)
echo ========================================
echo.

cd /d "%~dp0"

echo [1/3] Eliminando cache de compilacion...
if exist bin rd /s /q bin
if exist obj rd /s /q obj
echo Cache eliminada

echo.
echo [2/3] Compilando SlskDown (Release)...
dotnet build SlskDown.csproj -c Release
if errorlevel 1 (
    echo.
    echo ERROR DE COMPILACION
    pause
    exit /b 1
)

echo.
echo [3/3] Ejecutando SlskDown...
start "" "bin\Release\net9.0-windows\SlskDown.exe"

echo.
echo ========================================
echo APLICACION INICIADA
echo ========================================
pause
