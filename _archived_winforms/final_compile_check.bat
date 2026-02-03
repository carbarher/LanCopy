@echo off
echo ========================================
echo Compilacion Final - Verificando Errores
echo ========================================
echo.
dotnet clean SlskDown.csproj > nul 2>&1
dotnet build SlskDown.csproj -c Release > build_final.txt 2>&1
type build_final.txt | findstr /C:"error CS" /C:"Build succeeded" /C:"Build FAILED"
echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ COMPILACION EXITOSA
    echo.
    dir bin\Release\net8.0-windows\SlskDown.exe | findstr SlskDown.exe
) else (
    echo ❌ COMPILACION FALLIDA
    echo Ver build_final.txt para detalles
)
echo ========================================
