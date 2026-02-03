@echo off
echo ========================================
echo Compilando SlskDown con dotnet build
echo ========================================
echo.

dotnet build SlskDown.csproj -c Release > build_output.txt 2>&1

echo.
echo ========================================
echo Resultado de la compilacion:
echo ========================================
type build_output.txt

echo.
echo ========================================
echo Codigo de salida: %errorlevel%
echo ========================================

if %errorlevel% equ 0 (
    echo.
    echo ✅ COMPILACION EXITOSA
) else (
    echo.
    echo ❌ COMPILACION FALLIDA
)
