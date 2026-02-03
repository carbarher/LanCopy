@echo off
echo ========================================
echo Compilando SlskDown.csproj
echo ========================================
echo.
dotnet build SlskDown.csproj -c Release
echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [OK] Ejecutable generado exitosamente
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo [ERROR] No se genero el ejecutable
)
echo ========================================
pause
