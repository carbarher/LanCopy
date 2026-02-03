@echo off
echo Limpiando...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release -v minimal
echo.
echo Codigo de salida: %errorlevel%
echo.
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo EXITO - Ejecutable generado
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo ERROR - No se genero ejecutable
)
pause
