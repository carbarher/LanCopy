@echo off
echo ========================================
echo COMPILAR Y EJECUTAR - LIMPIO
echo ========================================
echo.

echo [1/4] Limpiando cache de compilacion...
del /s /q obj bin 2>nul
dotnet clean SlskDown.csproj >nul 2>&1

echo [2/4] Compilando desde cero...
dotnet build SlskDown.csproj --no-incremental

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo ERROR EN COMPILACION
    echo ========================================
    pause
    exit /b 1
)

echo.
echo [3/4] Compilacion exitosa!
echo [4/4] Ejecutando aplicacion...
echo.
echo ========================================
echo APLICACION INICIADA
echo ========================================
echo.

dotnet run --project SlskDown.csproj

pause
