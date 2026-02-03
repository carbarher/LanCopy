@echo off
title Desactivar Auto-Conexión
color 0E

echo ========================================
echo   DESACTIVAR AUTO-CONEXION
echo ========================================
echo.
echo Este script desactivará la conexión
echo automática al iniciar SlskDown.
echo.
echo Podrás conectar manualmente cuando
echo quieras desde el botón "Conectar".
echo.
pause

cd /d C:\Users\%USERNAME%\AppData\Roaming\SlskDown

if not exist config.json (
    echo ❌ No se encontró config.json
    pause
    exit /b 1
)

echo.
echo Modificando config.json...

powershell -Command "$config = Get-Content 'config.json' | ConvertFrom-Json; $config.autoConnect = $false; $config | ConvertTo-Json -Depth 10 | Set-Content 'config.json'"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Auto-conexión desactivada
    echo.
    echo Ahora SlskDown NO conectará automáticamente.
    echo Usa el botón "Conectar" cuando lo necesites.
) else (
    echo.
    echo ❌ Error al modificar config.json
)

echo.
pause
