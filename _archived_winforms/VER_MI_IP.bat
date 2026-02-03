@echo off
echo ========================================
echo   VERIFICAR MI IP ACTUAL
echo ========================================
echo.
echo IP Local (en tu red):
ipconfig | findstr /i "IPv4"
echo.
echo IP Publica (en Internet):
echo Consultando ipify.org...
powershell -Command "(Invoke-WebRequest -Uri 'https://api.ipify.org' -UseBasicParsing).Content"
echo.
echo.
echo Si cambias tu router, ejecuta este script
echo antes y despues para verificar el cambio.
echo.
pause
