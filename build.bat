@echo off
cd /d C:\p2p\SlskDown
echo Compilando SlskDown con dotnet...
dotnet build SlskDown.csproj -c Debug -v minimal
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo ✅ COMPILACION EXITOSA
    echo ========================================
) else (
    echo.
    echo ========================================
    echo ❌ ERRORES DE COMPILACION
    echo ========================================
)
pause
