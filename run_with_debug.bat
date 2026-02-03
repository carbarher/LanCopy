@echo off
cd /d "c:\p2p\SlskDown\bin\Release\net9.0-windows"
echo Ejecutando SlskDown con captura de errores...
echo.

start /wait SlskDown.exe

echo.
echo Aplicacion cerrada. Buscando logs...
echo.
dir /b constructor_*.txt 2>nul
dir /b close_attempt_*.txt 2>nul
dir /b *error*.txt 2>nul

echo.
pause
