@echo off
taskkill /F /IM SlskDown.exe 2>nul
del /Q "bin\Release\net8.0-windows\startup_log.txt" 2>nul
echo Ejecutando SlskDown...
start "" "bin\Release\net8.0-windows\SlskDown.exe"
timeout /t 3 /nobreak >nul
echo.
echo Aplicacion iniciada.
echo Haz clic en el boton Conectar y luego presiona una tecla aqui.
pause >nul
echo.
echo === LOG ===
type "bin\Release\net8.0-windows\startup_log.txt" 2>nul
echo.
pause
