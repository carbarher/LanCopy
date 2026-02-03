@echo off
echo Limpiando...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo Compilando...
dotnet build SlskDown.csproj 2>&1

echo.
echo Presiona cualquier tecla para continuar...
pause >nul

if exist bin\Debug\net8.0-windows\SlskDown.exe (
    echo.
    echo Ejecutando...
    bin\Debug\net8.0-windows\SlskDown.exe
) else (
    echo.
    echo ERROR: No se genero el ejecutable
    pause
)
