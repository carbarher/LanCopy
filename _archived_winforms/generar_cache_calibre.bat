@echo off
chcp 65001 >nul
echo ========================================
echo   Generar Caché de Archivos Calibre
echo ========================================
echo.

python generate_calibre_cache.py

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR al generar caché
    pause
    exit /b 1
)

echo.
pause
