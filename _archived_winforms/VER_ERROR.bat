@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo DIAGNOSTICO COMPLETO
echo ========================================
echo.

echo [1] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [OK] Ejecutable existe
    echo.
    
    echo [2] Intentando ejecutar...
    echo.
    cd bin\Release\net8.0-windows
    
    echo Si hay un error, aparecera aqui:
    echo ----------------------------------------
    SlskDown.exe
    echo ----------------------------------------
    echo.
    echo La aplicacion se cerro
) else (
    echo [ERROR] Ejecutable NO existe
    echo.
    echo Ubicacion esperada:
    echo %CD%\bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Ejecuta primero: COMPILAR_FORZADO.bat
)

echo.
echo.
echo COPIA TODO EL TEXTO DE ARRIBA Y ENVIAMELO
pause
