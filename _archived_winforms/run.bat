@echo off
echo ========================================
echo   SlskDown - Ejecutar
echo ========================================
echo.

if not exist "bin\Debug\net8.0-windows\SlskDown.exe" (
    echo ❌ ERROR: No se encuentra el ejecutable
    echo.
    echo Ejecuta primero: build.bat
    pause
    exit /b 1
)

bin\Debug\net8.0-windows\SlskDown.exe

echo.
echo ========================================
echo   Aplicacion cerrada
echo ========================================
pause
