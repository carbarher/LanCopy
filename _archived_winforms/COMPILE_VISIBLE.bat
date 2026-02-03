@echo off
title Compilando SlskDown
color 0A
cd /d c:\p2p\SlskDown

echo.
echo ========================================
echo   LIMPIANDO PROYECTO
echo ========================================
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo Limpieza completada.

echo.
echo ========================================
echo   COMPILANDO SLSKDOWN
echo ========================================
echo.

dotnet build SlskDown.csproj -c Release

echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo   COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutable creado en:
    echo bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause >nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo   COMPILACION FALLIDA
    echo ========================================
    echo.
    echo El ejecutable NO se creo.
    echo Revisa los errores arriba.
    echo.
    pause
)
