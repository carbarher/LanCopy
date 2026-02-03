@echo off
echo ========================================
echo   SlskDown - Compilar y Ejecutar
echo ========================================
echo.

echo [1/2] Compilando proyecto...
dotnet build SlskDown.csproj

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR: La compilacion fallo
    pause
    exit /b 1
)

echo.
echo ✅ Compilacion exitosa
echo.
echo [2/2] Ejecutando aplicacion...
echo.

bin\Debug\net8.0-windows\SlskDown.exe

echo.
echo ========================================
echo   Aplicacion cerrada
echo ========================================
pause
