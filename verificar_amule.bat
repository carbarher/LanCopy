@echo off
echo ========================================
echo Verificacion de aMule
echo ========================================
echo.

echo [1/4] Verificando instalacion...
if exist "C:\Program Files\aMule\amuled.exe" (
    echo ✅ aMule instalado correctamente
) else (
    echo ❌ aMule NO instalado
    echo    Descarga de: https://www.amule.org/
    goto :end
)
echo.

echo [2/4] Verificando configuracion...
if exist "%USERPROFILE%\.aMule\amule.conf" (
    echo ✅ Archivo de configuracion existe
    echo    Ubicacion: %USERPROFILE%\.aMule\amule.conf
) else (
    echo ⚠️ Archivo de configuracion NO existe
    echo    Ejecuta: configurar_amule.bat
)
echo.

echo [3/4] Verificando daemon corriendo...
netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ aMule daemon esta corriendo
    echo    Puerto 4712: LISTENING
    netstat -ano | findstr :4712
) else (
    echo ⚠️ aMule daemon NO esta corriendo
    echo    Iniciar con: "C:\Program Files\aMule\amuled.exe"
)
echo.

echo [4/4] Verificando proceso...
tasklist | findstr /I "amule" >nul
if %errorlevel% equ 0 (
    echo ✅ Proceso aMule encontrado
    tasklist | findstr /I "amule"
) else (
    echo ⚠️ Proceso aMule NO encontrado
)
echo.

echo ========================================
echo Resumen
echo ========================================
if exist "C:\Program Files\aMule\amuled.exe" (
    echo ✅ Instalacion: OK
) else (
    echo ❌ Instalacion: FALTA
)

if exist "%USERPROFILE%\.aMule\amule.conf" (
    echo ✅ Configuracion: OK
) else (
    echo ⚠️ Configuracion: FALTA
)

netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ Daemon: CORRIENDO
) else (
    echo ⚠️ Daemon: DETENIDO
)
echo ========================================
echo.

:end
pause
