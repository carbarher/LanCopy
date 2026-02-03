@echo off
echo ========================================
echo Compilacion FINAL de SlskDown
echo ========================================
echo.
echo Limpiando...
dotnet clean SlskDown.csproj
echo.
echo Compilando en modo Release...
dotnet build SlskDown.csproj -c Release -v normal
echo.
echo ========================================
echo Verificando resultado...
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo [EXITO] Ejecutable generado correctamente
    echo.
    dir bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Puedes ejecutarlo con: slsk.bat
    echo.
) else (
    echo.
    echo [ERROR] No se genero el ejecutable
    echo Revisa los errores arriba
    echo.
)
pause
