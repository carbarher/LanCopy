@echo off
cd /d c:\p2p\SlskDown\bin\Release\net8.0-windows

if not exist SlskDown.exe (
    echo ERROR: SlskDown.exe no existe
    echo Primero compila con COMPILAR_FORZADO.bat
    pause
    exit
)

echo ========================================
echo EJECUTANDO CON CAPTURA DE ERRORES
echo ========================================
echo.

echo Iniciando SlskDown...
echo Si hay un error, se mostrara aqui:
echo.

SlskDown.exe 2>&1

echo.
echo ========================================
echo La aplicacion se cerro
echo ========================================
echo.
echo Si viste algun error arriba, copia el mensaje
pause
