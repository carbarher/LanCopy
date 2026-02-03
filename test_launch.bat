@echo off
echo Ejecutando SlskDown con captura de errores...
cd /d "c:\p2p\SlskDown\bin\Release\net9.0-windows"
SlskDown.exe 2>&1 | tee launch_output.txt
echo.
echo Exit code: %ERRORLEVEL%
pause
