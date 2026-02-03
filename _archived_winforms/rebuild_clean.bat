@echo off
echo ========================================
echo   SlskDown - Rebuild Limpio
echo ========================================
echo.

echo [1/4] Eliminando archivos temporales...
del /F /Q *temp*.cs 2>nul
echo ✅ Archivos temporales eliminados

echo.
echo [2/4] Limpiando carpetas bin y obj...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
echo ✅ Carpetas limpiadas

echo.
echo [3/4] Limpiando proyecto...
dotnet clean --nologo

echo.
echo [4/4] Compilando desde cero...
dotnet build SlskDown.csproj --no-incremental --nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR: La compilacion fallo
    pause
    exit /b 1
)

echo.
echo ✅ Compilacion exitosa
echo.
echo Ejecutando aplicacion...
echo.

bin\Debug\net8.0-windows\SlskDown.exe

echo.
echo ========================================
echo   Aplicacion cerrada
echo ========================================
pause
