@echo off
echo ========================================
echo  EJECUTANDO SlskDown
echo ========================================
echo.

echo Iniciando aplicación...
start "" "bin\Release\net8.0-windows\SlskDown.exe"

echo.
echo Esperando 5 segundos para que inicie...
timeout /t 5 /nobreak >nul

echo.
echo ========================================
echo  MOSTRANDO ÚLTIMAS LÍNEAS DEL LOG
echo ========================================
echo.

powershell -Command "if (Test-Path 'bin\Release\net8.0-windows\auto_log.txt') { Get-Content 'bin\Release\net8.0-windows\auto_log.txt' -Tail 30 } else { Write-Host 'No hay log todavía' }"

echo.
echo ========================================
echo.
echo La aplicación está ejecutándose.
echo Revisa la ventana de SlskDown.
echo.
pause
