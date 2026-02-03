@echo off
echo ========================================
echo   MONITOREANDO LOGS DE SLSKDOWN
echo ========================================
echo.
echo Buscando archivo de log mas reciente...
echo.

cd /d "%APPDATA%\SlskDown\logs"

for /f "delims=" %%i in ('dir /b /od slskdown_*.log 2^>nul') do set LASTLOG=%%i

if "%LASTLOG%"=="" (
    echo No se encontraron logs
    pause
    exit /b
)

echo Archivo: %LASTLOG%
echo.
echo Presiona Ctrl+C para salir
echo ========================================
echo.

powershell -Command "Get-Content '%LASTLOG%' -Tail 30 -Wait"
