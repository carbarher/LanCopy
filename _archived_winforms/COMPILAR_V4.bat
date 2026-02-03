@echo off
echo ========================================
echo   SlskDown v4.1 - Compilacion
echo ========================================
echo.

REM Cerrar procesos existentes
echo [1/3] Cerrando procesos...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 1 /nobreak >nul

REM Compilar
echo [2/3] Compilando...
dotnet build SlskDown.csproj -c Release --nologo --verbosity minimal

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR DE COMPILACION
    pause
    exit /b 1
)

echo [3/3] Compilacion exitosa
echo.
echo ========================================
echo   ✅ LISTO
echo ========================================
echo.
echo Para ejecutar: bin\Release\net8.0-windows\SlskDown.exe
echo.
pause
