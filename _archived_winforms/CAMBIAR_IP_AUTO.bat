@echo off
REM Script automatico para cambiar IP (sin pausas)
echo [%TIME%] Reiniciando adaptador de red para cambiar IP...

REM Deshabilitando adaptador
netsh interface set interface "Ethernet" disabled 2>nul
if errorlevel 1 (
    netsh interface set interface "Wi-Fi" disabled 2>nul
)
timeout /t 5 /nobreak >nul

REM Habilitando adaptador
netsh interface set interface "Ethernet" enabled 2>nul
if errorlevel 1 (
    netsh interface set interface "Wi-Fi" enabled 2>nul
)
timeout /t 5 /nobreak >nul

echo [%TIME%] Adaptador reiniciado. Verificando IP...
ipconfig | findstr /i "IPv4"
echo [%TIME%] Proceso completado.
exit /b 0
