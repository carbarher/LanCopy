@echo off
echo ========================================
echo VERIFICACION FINAL DE COMPILACION
echo ========================================
echo.

dotnet build SlskDown.csproj --configuration Release --no-incremental 2>&1 | findstr /C:"Build succeeded" /C:"Build FAILED" /C:"Error(s)" /C:"Warning(s)"

echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ EJECUTABLE GENERADO
    echo bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo ❌ EJECUTABLE NO ENCONTRADO
)
echo ========================================
pause
