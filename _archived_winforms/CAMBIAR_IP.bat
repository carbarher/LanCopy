@echo off
echo ========================================
echo   REINICIAR CONEXION PARA CAMBIAR IP
echo ========================================
echo.
echo Este script reiniciara tu adaptador de red
echo para intentar obtener una nueva IP del ISP.
echo.
echo NOTA: Solo funciona si tu ISP asigna IPs dinamicas.
echo.
pause

echo.
echo Deshabilitando adaptador de red...
netsh interface set interface "Ethernet" disabled
timeout /t 5 /nobreak

echo Habilitando adaptador de red...
netsh interface set interface "Ethernet" enabled
timeout /t 5 /nobreak

echo.
echo Verificando nueva IP...
ipconfig | findstr /i "IPv4"

echo.
echo Listo! Si tu IP cambio, ya puedes intentar reconectar.
pause
