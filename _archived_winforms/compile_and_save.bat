@echo off
echo ========================================
echo COMPILAR Y GUARDAR EN GIT
echo ========================================
echo.

REM 1. Guardar en Git ANTES de compilar
echo [1/3] Guardando cambios en Git...
call auto_commit.bat

echo.
echo [2/3] Compilando proyecto...
dotnet build SlskDown.csproj -c Release

echo.
echo [3/3] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✓ Ejecutable generado correctamente
    echo.
    echo ========================================
    echo Ejecutando aplicacion...
    echo ========================================
    echo.
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ✗ ERROR: No se genero el ejecutable
    echo Revisa los errores de compilacion
    pause
)
