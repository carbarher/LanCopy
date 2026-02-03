@echo off
title SlskDown - Nueva Versión
color 0A

echo ========================================
echo   EJECUTAR NUEVA VERSION
echo ========================================
echo.

REM Matar cualquier instancia anterior
taskkill /F /IM SlskDown.exe 2>nul

echo Compilando versión nueva...
cd /d c:\p2p\SlskDown
dotnet build SlskDown.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ Error en compilación
    pause
    exit /b 1
)

echo.
echo ✅ Compilación exitosa
echo.
echo Ejecutando versión nueva...
echo Ruta: c:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe
echo.

start "" "c:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe"

echo.
echo ✅ SlskDown iniciado
echo.
echo Verifica el log para confirmar:
echo - Timeout de 30 segundos (no 5)
echo - Conexión exitosa
echo.
pause
