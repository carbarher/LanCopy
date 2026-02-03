@echo off
echo Compilando SlskDown...
dotnet build -c Release -v n
echo.
echo Compilacion terminada. Codigo de salida: %ERRORLEVEL%
pause
