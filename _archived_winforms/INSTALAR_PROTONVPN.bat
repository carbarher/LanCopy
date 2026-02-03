@echo off
echo ========================================
echo   INSTALADOR DE PROTONVPN CLI
echo ========================================
echo.

:: Verificar si Python esta instalado
echo [1/4] Verificando Python...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python NO esta instalado
    echo.
    echo [INFO] Necesitas instalar Python primero:
    echo    1. Ve a: https://www.python.org/downloads/
    echo    2. Descarga Python 3.x
    echo    3. Durante la instalacion, marca "Add Python to PATH"
    echo    4. Vuelve a ejecutar este script
    echo.
    pause
    exit /b 1
)

python --version
echo [OK] Python instalado
echo.

:: Verificar si pip esta disponible
echo [2/4] Verificando pip...
pip --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] pip NO esta disponible
    echo [INFO] Instala pip ejecutando: python -m ensurepip --upgrade
    pause
    exit /b 1
)
echo [OK] pip disponible
echo.

:: Instalar ProtonVPN CLI
echo [3/4] Instalando ProtonVPN CLI...
echo    Esto puede tomar unos minutos...
pip install protonvpn-cli
if %errorlevel% neq 0 (
    echo [ERROR] Error instalando ProtonVPN CLI
    pause
    exit /b 1
)
echo [OK] ProtonVPN CLI instalado
echo.

:: Verificar instalacion
echo [4/4] Verificando instalacion...
protonvpn --version >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] ProtonVPN instalado correctamente
    protonvpn --version
) else (
    protonvpn-cli --version >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] ProtonVPN instalado correctamente
        protonvpn-cli --version
    ) else (
        echo [WARN] Instalacion completada pero el comando no esta disponible
        echo [INFO] Puede que necesites reiniciar la terminal o el sistema
    )
)
echo.

:: Instrucciones de configuracion
echo ========================================
echo   CONFIGURACION
echo ========================================
echo.
echo Ahora necesitas configurar tu cuenta ProtonVPN:
echo.
echo 1. Si NO tienes cuenta, creala en:
echo    https://account.protonvpn.com/signup
echo    (Plan gratuito disponible)
echo.
echo 2. Ejecuta: protonvpn init
echo    o:       protonvpn-cli init
echo.
echo 3. Ingresa tu usuario y contrasena de ProtonVPN
echo.
echo 4. Selecciona el protocolo (OpenVPN recomendado)
echo.
echo 5. Listo! SlskDown podra usar VPN automaticamente
echo.
echo ========================================
pause
