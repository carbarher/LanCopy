@echo off
echo ========================================
echo COMPILANDO FIX DE RECONEXION INFINITA
echo ========================================
echo.

echo Limpiando compilacion anterior...
dotnet clean SlskDown.csproj -v q

echo.
echo Compilando con los cambios...
dotnet build SlskDown.csproj -c Release -v m

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: La compilacion fallo
    pause
    exit /b 1
)

echo.
echo ========================================
echo COMPILACION EXITOSA
echo ========================================
echo.
echo Cambios aplicados (3 problemas criticos resueltos):
echo.
echo 1. isReconnecting se resetea correctamente en todos los returns
echo    - Todos los resets protegidos con lock(this)
echo    - Se evitan race conditions
echo.
echo 2. Cooldown de 600s solo se aplica despues del 2do intento
echo    - Primer intento usa backoff normal (5-10s)
echo.
echo 3. isConnecting se resetea durante esperas largas (CRITICO)
echo    - Permite que CheckAndReconnect() se ejecute durante esperas
echo    - Aplica en esperas de 600s, 15min y fallos TCP
echo.
echo EJECUTANDO APLICACION...
echo.
start "" "bin\Release\net8.0-windows\SlskDown.exe"

echo.
echo ========================================
echo INSTRUCCIONES DE PRUEBA:
echo ========================================
echo.
echo 1. La aplicacion deberia conectar en 5-10 segundos (NO 600s)
echo.
echo 2. Si hay timeout, observa los logs durante la espera:
echo    - "[DEBUG] isConnecting reseteado a false antes de espera..."
echo    - "[DEBUG] CheckAndReconnect() llamado" (se ejecuta durante espera)
echo    - "[CheckAndReconnect] Finally: isReconnecting establecido en false"
echo.
echo 3. Ya NO deberia mostrar:
echo    - "Reconexion ignorada - isReconnecting=True"
echo    - "Reconexion ignorada - isConnecting=True"
echo    - "Usando cooldown recomendado: 600s" en 1er o 2do intento
echo.
pause
