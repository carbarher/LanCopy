@echo off
echo Limpiando directorios de compilacion...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo Compilando proyecto...
dotnet build SlskDown.csproj -c Release --no-incremental -v n > build_output.txt 2>&1

echo.
type build_output.txt

echo.
echo Verificando resultado...
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo COMPILACION EXITOSA
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo COMPILACION FALLIDA - No se genero el ejecutable
)

echo.
pause
