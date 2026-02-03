@echo off
echo ========================================
echo   VERIFICAR SI TIENES IP DINAMICA
echo ========================================
echo.
echo Tu IP publica actual es:
powershell -Command "(Invoke-WebRequest -Uri 'https://api.ipify.org' -UseBasicParsing).Content"
echo.
echo.
echo INSTRUCCIONES:
echo 1. Anota esta IP
echo 2. Apaga tu router 10 minutos
echo 3. Enciende el router
echo 4. Ejecuta este script de nuevo
echo 5. Si la IP cambio = tienes IP DINAMICA
echo 6. Si la IP es igual = tienes IP ESTATICA
echo.
echo Si tienes IP ESTATICA, necesitas usar VPN.
echo.
pause
