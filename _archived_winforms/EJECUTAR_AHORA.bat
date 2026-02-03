@echo off
echo ========================================
echo EJECUTANDO SLSKDOWN
echo ========================================
echo.
echo Si se congela al clicar botones:
echo - Presiona Ctrl+C aqui
echo - Enviame el mensaje de error
echo.
pause

cd /d c:\p2p\SlskDown\bin\Release\net8.0-windows
start "" SlskDown.exe

echo.
echo Aplicacion ejecutada.
echo Verifica si se abre la ventana.
echo.
pause
