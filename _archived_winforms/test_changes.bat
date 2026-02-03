@echo off
echo ========================================
echo VERIFICANDO CAMBIOS EN SLSKDOWN
echo ========================================
echo.

echo Cerrando SlskDown si esta abierto...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo Ejecutando SlskDown desde:
echo %CD%\bin\Release\net9.0-windows\SlskDown.exe
echo.
echo VERIFICA EN LA APP:
echo 1. Checkboxes en LINEA HORIZONTAL (no verticales)
echo 2. Boton rojo "DETENER" entre INICIAR y PURGAR
echo 3. Boton azul "COPIAR LOG"
echo 4. Al conectar: "Red distribuida: HABILITADA"
echo.
pause

start "" "%CD%\bin\Release\net9.0-windows\SlskDown.exe"
