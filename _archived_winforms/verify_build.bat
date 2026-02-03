@echo off
dotnet build SlskDown.csproj -c Release > build_output.txt 2>&1
type build_output.txt | findstr /C:"error CS" /C:"Build succeeded" /C:"Build FAILED"
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ✅ COMPILACION EXITOSA
) else (
    echo.
    echo ❌ COMPILACION FALLIDA
)
