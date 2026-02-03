@echo off
echo ========================================
echo Compilacion Final v2 - Verificando
echo ========================================
echo.
dotnet clean SlskDown.csproj > nul 2>&1
echo Limpieza completada...
echo.
dotnet build SlskDown.csproj -c Release > build_v2.txt 2>&1
echo.
echo === ERRORES ===
type build_v2.txt | findstr /C:"error CS"
echo.
echo === RESULTADO ===
type build_v2.txt | findstr /C:"Build succeeded" /C:"Build FAILED"
echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ COMPILACION EXITOSA
    dir bin\Release\net8.0-windows\SlskDown.exe | findstr SlskDown.exe
) else (
    echo ❌ COMPILACION FALLIDA
    echo Ver build_v2.txt para detalles completos
)
echo ========================================
