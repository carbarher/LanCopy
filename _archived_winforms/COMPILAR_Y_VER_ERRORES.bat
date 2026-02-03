@echo off
echo ========================================
echo Compilando y mostrando SOLO errores...
echo ========================================
echo.
dotnet build SlskDown.csproj -c Release 2>&1 | findstr /C:"error CS"
echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [EXITO] Ejecutable generado
) else (
    echo [ERROR] Compilacion fallida
)
echo ========================================
pause
