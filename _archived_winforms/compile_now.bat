@echo off
cd /d c:\p2p\SlskDown
echo Limpiando...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release
echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo EXITO: Ejecutable generado
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ERROR: No se genero ejecutable
    echo.
    echo Verificando carpetas...
    dir bin /s
)
pause
