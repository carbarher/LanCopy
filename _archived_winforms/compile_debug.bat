@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
dotnet build SlskDown.csproj -c Release > compile_output.txt 2>&1
type compile_output.txt
echo.
echo Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo EXITO: Ejecutable generado
) else (
    echo ERROR: No se genero ejecutable
)
pause
