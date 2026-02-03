@echo off
cd /d c:\p2p\SlskDown\bin\Release\net8.0-windows

if exist SlskDown.exe (
    echo Iniciando SlskDown...
    start "" SlskDown.exe
) else (
    echo ERROR: SlskDown.exe no encontrado
    echo Primero debes compilar con COMPILAR_Y_ESPERAR.bat
    pause
)
