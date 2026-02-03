@echo off
echo ========================================
echo Compilando SlskDown - Version Limpia
echo ========================================
echo.
dotnet build SlskDown.csproj -c Release
echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo [EXITO] Ejecutable generado:
    dir bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Puedes ejecutarlo con: slsk.bat
) else (
    echo.
    echo [ERROR] No se genero el ejecutable
    echo Revisa los errores arriba
)
echo ========================================
pause
