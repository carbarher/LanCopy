@echo off
REM SlskDown - Lanzador rapido con compilacion

REM Cerrar instancia anterior si existe
taskkill /F /IM SlskDown.exe 2>nul >nul
timeout /t 1 >nul 2>nul

echo ==========================================
echo   COMPILANDO SlskDown
echo ==========================================
cd /d "%~dp0SlskDown"
dotnet build -c Release

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo compilar
    pause
    exit /b 1
)

echo.
echo ==========================================
echo   INICIANDO SlskDown
echo ==========================================
set EXE_PATH=%~dp0SlskDown\bin\Release\net8.0-windows\SlskDown.exe
start "" "%EXE_PATH%"
