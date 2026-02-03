@echo off
echo ========================================
echo   COMPILACION - SlskDown v4.1
echo ========================================
echo.

REM Cerrar procesos existentes
echo [1/3] Cerrando procesos existentes...
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
echo   ✅ LISTO PARA EJECUTAR
echo ========================================
)

echo ✅ Compilación Release exitosa

echo.
echo [5/5] Iniciando SlskDown...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ Ejecutable encontrado
    echo.
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
    echo ✅ SlskDown iniciado!
    echo.
    timeout /t 2 /nobreak >nul
) else (
    echo ❌ ERROR: No se generó el ejecutable
    pause
)
