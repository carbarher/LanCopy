@echo off
chcp 65001 >nul
echo ========================================
echo  LIMPIANDO LOGS Y EJECUTANDO
echo ========================================
echo.

echo [1/3] Cerrando instancias anteriores...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/3] Limpiando logs...
del /Q "bin\Release\net8.0-windows\startup_log.txt" 2>nul
echo      OK - Logs limpiados

echo [3/3] Ejecutando aplicacion...
start "" "bin\Release\net8.0-windows\SlskDown.exe"
timeout /t 3 /nobreak >nul

echo.
echo ========================================
echo  MOSTRANDO LOG DE INICIO
echo ========================================
echo.
type "bin\Release\net8.0-windows\startup_log.txt" 2>nul

echo.
echo ========================================
echo.
echo La aplicacion esta ejecutandose.
echo.
echo PRUEBA ESTO:
echo 1. Haz clic en el boton Conectar
echo 2. Vuelve aqui y presiona cualquier tecla
echo 3. Veremos el log actualizado
echo.
pause

echo.
echo ========================================
echo  LOG ACTUALIZADO
echo ========================================
echo.
type "bin\Release\net8.0-windows\startup_log.txt" 2>nul

echo.
pause
