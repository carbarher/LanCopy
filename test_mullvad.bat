@echo off
echo ========================================
echo    PRUEBA DE MULLVAD VPN CLI
echo ========================================
echo.

echo [1/4] Verificando instalacion...
if exist "C:\Program Files\Mullvad VPN\resources\mullvad.exe" (
    echo [OK] Mullvad instalado en: C:\Program Files\Mullvad VPN\resources\mullvad.exe
) else (
    echo [ERROR] Mullvad NO esta instalado
    pause
    exit /b 1
)
echo.

echo [2/4] Verificando estado del servicio...
"C:\Program Files\Mullvad VPN\resources\mullvad.exe" status
echo.

echo [3/4] Verificando version...
"C:\Program Files\Mullvad VPN\resources\mullvad.exe" version
echo.

echo [4/4] Verificando cuenta...
"C:\Program Files\Mullvad VPN\resources\mullvad.exe" account get
echo.

echo ========================================
echo    PRUEBA COMPLETADA
echo ========================================
echo.
echo Si ves "Logged in" arriba, Mullvad esta configurado correctamente.
echo Si ves "Not logged in", necesitas hacer login con tu numero de cuenta.
echo.
pause
