@echo off
echo Limpiando proyecto...
dotnet clean
echo.
echo Compilando...
dotnet build -c Release
echo.
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo EXITO - Ejecutable generado
) else (
    echo ERROR - No se genero ejecutable
)
pause
