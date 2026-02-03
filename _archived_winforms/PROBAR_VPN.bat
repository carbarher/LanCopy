@echo off
chcp 65001 >nul
echo ========================================
echo   PRUEBA DE PROTONVPN
echo ========================================
echo.

:: Verificar instalación
echo [1/5] Verificando instalación...
set VPN_CMD=
protonvpn --version >nul 2>&1
if %errorlevel% equ 0 (
    set VPN_CMD=protonvpn
    echo ✅ Comando encontrado: protonvpn
) else (
    protonvpn-cli --version >nul 2>&1
    if %errorlevel% equ 0 (
        set VPN_CMD=protonvpn-cli
        echo ✅ Comando encontrado: protonvpn-cli
    ) else (
        echo ❌ ProtonVPN CLI no está instalado
        echo 💡 Ejecuta: INSTALAR_PROTONVPN.bat
        pause
        exit /b 1
    )
)
echo.

:: Mostrar versión
echo [2/5] Versión instalada:
%VPN_CMD% --version
echo.

:: Verificar configuración
echo [3/5] Verificando configuración...
%VPN_CMD% status
if %errorlevel% neq 0 (
    echo ⚠️ ProtonVPN no está configurado
    echo.
    echo 💡 Configúralo ejecutando: %VPN_CMD% init
    echo    Necesitarás tu usuario y contraseña de ProtonVPN
    echo.
    pause
    exit /b 1
)
echo.

:: Mostrar IP actual
echo [4/5] Tu IP actual:
curl -s https://api.ipify.org
echo.
echo.

:: Preguntar si quiere probar conexión
echo [5/5] ¿Quieres probar conectar a VPN? (S/N)
set /p RESPUESTA="> "
if /i "%RESPUESTA%" neq "S" (
    echo.
    echo ℹ️ Prueba cancelada
    pause
    exit /b 0
)

echo.
echo 🔐 Conectando a servidor gratuito...
%VPN_CMD% connect --fastest
if %errorlevel% neq 0 (
    echo ❌ Error conectando
    pause
    exit /b 1
)

echo.
echo ✅ Conectado! Tu nueva IP:
timeout /t 3 /nobreak >nul
curl -s https://api.ipify.org
echo.
echo.

echo ¿Desconectar VPN? (S/N)
set /p RESPUESTA2="> "
if /i "%RESPUESTA2%" equ "S" (
    echo.
    echo 🔓 Desconectando...
    %VPN_CMD% disconnect
    echo ✅ Desconectado
)

echo.
echo ========================================
pause
