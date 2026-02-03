@echo off
echo ========================================
echo   SlskDown - Compilar
echo ========================================
echo.

dotnet build SlskDown.csproj

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR: La compilacion fallo
    pause
    exit /b 1
)

echo.
echo ✅ Compilacion exitosa
pause
