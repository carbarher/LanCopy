@echo off
echo === PROBANDO WINDSCRIBE CLI ===
echo.
echo Ruta del CLI: "C:\Program Files\Windscribe\windscribe-cli.exe"
echo.
echo 1. Verificando versión...
"C:\Program Files\Windscribe\windscribe-cli.exe" --version
echo.
echo 2. Verificando estado actual...
"C:\Program Files\Windscribe\windscribe-cli.exe" status
echo.
echo 3. Verificando cuenta/datos...
"C:\Program Files\Windscribe\windscribe-cli.exe" account
echo.
echo 4. Verificando firewall...
"C:\Program Files\Windscribe\windscribe-cli.exe" firewall status
echo.
echo 5. Listando locations disponibles...
"C:\Program Files\Windscribe\windscribe-cli.exe" locations
echo.
pause
