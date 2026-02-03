@echo off
echo ========================================
echo COMPILANDO SlskDown v4.1
echo ========================================
echo.

echo Limpiando...
dotnet clean SlskDown.csproj
echo.

echo Compilando en Release...
dotnet build SlskDown.csproj -c Release -v n
echo.

echo Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown_NEW.exe" (
    echo ✓ Ejecutable generado correctamente
    echo Ubicacion: bin\Release\net8.0-windows\SlskDown_NEW.exe
    echo.
    echo Ejecutando...
    start bin\Release\net8.0-windows\SlskDown_NEW.exe
) else (
    echo ✗ ERROR: No se genero el ejecutable
    echo.
    echo Intentando con dotnet run...
    dotnet run --project SlskDown.csproj
)

pause
