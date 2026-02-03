@echo off
echo ========================================
echo EJECUTANDO SLSKDOWN
echo ========================================
echo.
echo Si la app se congela:
echo 1. NO uses los botones de seleccion todavia
echo 2. Solo verifica que la ventana se abre
echo 3. Cierra la app con la X
echo.
echo Presiona cualquier tecla para ejecutar...
pause >nul

cd /d c:\p2p\SlskDown\bin\Release\net8.0-windows
start "" SlskDown.exe

echo.
echo App ejecutada. Verifica si se abre la ventana.
echo.
pause
