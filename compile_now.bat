@echo off
cd /d c:\p2p\SlskDown
echo Compilando SlskDown...
dotnet build SlskDown.csproj -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ COMPILACION EXITOSA
    echo.
    echo Ejecutable en: bin\Release\net9.0-windows\SlskDown.exe
) else (
    echo.
    echo ❌ ERROR DE COMPILACION
)
