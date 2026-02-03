@echo off
echo ========================================
echo Verificacion eMule Instalado
echo ========================================
echo.

echo [1/3] Verificando proceso eMule...
tasklist | findstr /I "emule" >nul
if %errorlevel% equ 0 (
    echo ✅ eMule esta corriendo
    tasklist | findstr /I "emule"
) else (
    echo ⚠️ eMule NO esta corriendo
    echo    Por favor inicia eMule
)
echo.

echo [2/3] Verificando puerto EC (4712)...
netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ Puerto 4712 esta abierto (EC configurado)
    netstat -ano | findstr :4712
) else (
    echo ⚠️ Puerto 4712 NO esta abierto
    echo.
    echo    ACCION REQUERIDA:
    echo    1. Abrir eMule
    echo    2. Ir a: Preferencias ^> Conexion Remota
    echo    3. Activar: "Aceptar conexiones externas"
    echo    4. Puerto: 4712
    echo    5. Contraseña: [elegir una]
    echo    6. Aplicar y reiniciar eMule
)
echo.

echo [3/3] Verificando instalacion eMule...
if exist "C:\Program Files\eMule\emule.exe" (
    echo ✅ eMule encontrado en: C:\Program Files\eMule\
) else if exist "C:\Program Files (x86)\eMule\emule.exe" (
    echo ✅ eMule encontrado en: C:\Program Files (x86)\eMule\
) else (
    echo ℹ️ eMule instalado en ubicacion personalizada
)
echo.

echo ========================================
echo Resumen
echo ========================================
tasklist | findstr /I "emule" >nul
if %errorlevel% equ 0 (
    echo ✅ eMule: CORRIENDO
) else (
    echo ⚠️ eMule: DETENIDO
)

netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ Puerto EC: CONFIGURADO
    echo.
    echo 🎉 LISTO PARA USAR CON SLSKDOWN
    echo.
    echo Siguiente paso:
    echo 1. Reiniciar SlskDown
    echo 2. Ir a Configuracion
    echo 3. Activar "Habilitar eMule/ed2k"
    echo 4. Ingresar contraseña EC
    echo 5. Reiniciar SlskDown
    echo 6. ¡Disfrutar multi-red!
) else (
    echo ⚠️ Puerto EC: NO CONFIGURADO
    echo.
    echo Configura External Connections en eMule
)
echo ========================================
echo.
pause
