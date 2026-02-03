@echo off
echo ========================================
echo Verificacion del Sistema Multi-Red
echo ========================================
echo.

echo [1/5] Verificando binario SlskDown...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ SlskDown.exe encontrado
    dir bin\Release\net8.0-windows\SlskDown.exe | findstr /C:"SlskDown.exe"
) else (
    echo ❌ SlskDown.exe NO encontrado
)
echo.

echo [2/5] Verificando aMule daemon...
netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ aMule daemon corriendo en puerto 4712
    netstat -ano | findstr :4712
) else (
    echo ⚠️ aMule daemon NO esta corriendo
    echo    Puerto 4712 no esta en uso
)
echo.

echo [3/5] Verificando archivos de configuracion...
if exist "*.config" (
    echo ✅ Archivos de configuracion encontrados
    dir *.config /B
) else (
    echo ⚠️ No se encontraron archivos .config
)
echo.

echo [4/5] Verificando documentacion...
if exist "..\GUIA_USUARIO_MULTI_RED.md" (
    echo ✅ Documentacion disponible
) else (
    echo ⚠️ Documentacion no encontrada
)
echo.

echo [5/5] Verificando backup...
if exist "backup_antes_multi_red\" (
    echo ✅ Backup existe
    dir backup_antes_multi_red\ /B
) else (
    echo ⚠️ No hay backup
    echo    Recomendacion: Crear backup antes de probar
)
echo.

echo ========================================
echo Resumen
echo ========================================
echo.
echo Estado del sistema:
netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ aMule: LISTO
) else (
    echo ⏳ aMule: PENDIENTE
)

if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ SlskDown: COMPILADO
) else (
    echo ❌ SlskDown: NO COMPILADO
)

if exist "backup_antes_multi_red\" (
    echo ✅ Backup: CREADO
) else (
    echo ⏳ Backup: PENDIENTE
)

echo.
echo ========================================
pause
