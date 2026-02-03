@echo off
REM Script de rebuild completo para SlskDown
REM Fuerza la recarga del proyecto

cd /d "%~dp0"

echo ==========================================
echo   REBUILD COMPLETO de SlskDown
echo ==========================================
echo.

echo [1/5] Cerrando SlskDown si esta abierto...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo [2/5] Limpiando proyecto...
dotnet clean

echo.
echo [3/5] Restaurando dependencias...
dotnet restore

echo.
echo [4/5] Compilando en Release...
dotnet build -c Release

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo compilar
    pause
    exit /b 1
)

echo.
echo ==========================================
echo   COMPILACION EXITOSA
echo ==========================================
echo.
echo Ejecutable: bin\Release\net9.0-windows\SlskDown.exe
echo.
pause
