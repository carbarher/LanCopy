@echo off
echo === CERRANDO SLSKDOWN ===
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo === BORRANDO LOG ANTIGUO ===
del /q logs\slskdown-2025-11-03.txt 2>nul

echo.
echo === EJECUTANDO NUEVA VERSION ===
start "" "bin\Release\net8.0-windows\SlskDown.exe"

echo.
echo Espera a que aparezca la ventana...
echo 1. Haz click en CONECTAR
echo 2. Espera 5 segundos
echo 3. Busca: asimov
echo 4. Presiona cualquier tecla aqui para ver el log
pause

echo.
echo === LOG NUEVO ===
type logs\slskdown-2025-11-03.txt
pause
