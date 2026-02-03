@echo off
cd /d c:\p2p\SlskDown\bin\Release\net8.0-windows

echo ========================================
echo DIAGNOSTICO COMPLETO - SLSKDOWN
echo ========================================
echo.

REM Limpiar archivos de error anteriores
del c:\p2p\error_log.txt 2>nul
del c:\p2p\program_debug.txt 2>nul
del c:\p2p\slskdown_debug.txt 2>nul
del c:\p2p\click_debug.txt 2>nul

echo [1/4] Verificando ejecutable...
if exist SlskDown.exe (
    echo    OK - Ejecutable existe
    dir SlskDown.exe | find "SlskDown.exe"
) else (
    echo    ERROR - Ejecutable NO existe
    pause
    exit /b 1
)

echo.
echo [2/4] Verificando DLLs...
if exist Soulseek.dll (
    echo    OK - Soulseek.dll existe
) else (
    echo    ERROR - Soulseek.dll NO existe
)

echo.
echo [3/4] Ejecutando con captura de errores...
echo    Iniciando SlskDown.exe...
echo    (Si no se abre en 5 segundos, hay un crash)
echo.

SlskDown.exe 2>&1

echo.
echo ========================================
echo [4/4] VERIFICANDO ARCHIVOS DE ERROR
echo ========================================

if exist c:\p2p\error_log.txt (
    echo.
    echo *** ERROR_LOG.TXT ENCONTRADO ***
    type c:\p2p\error_log.txt
) else (
    echo    No se creo error_log.txt
)

if exist c:\p2p\program_debug.txt (
    echo.
    echo *** PROGRAM_DEBUG.TXT ENCONTRADO ***
    type c:\p2p\program_debug.txt
) else (
    echo    No se creo program_debug.txt - NO LLEGO AL MAIN
)

if exist c:\p2p\slskdown_debug.txt (
    echo.
    echo *** SLSKDOWN_DEBUG.TXT ENCONTRADO ***
    type c:\p2p\slskdown_debug.txt
) else (
    echo    No se creo slskdown_debug.txt - NO LLEGO AL CONSTRUCTOR
)

echo.
echo ========================================
echo DIAGNOSTICO COMPLETADO
echo ========================================
pause
