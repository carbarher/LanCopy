@echo off
echo Compilando SlskDown...
dotnet build SlskDown.csproj -c Release --no-incremental -v q
echo.
echo Codigo de salida: %ERRORLEVEL%
if %ERRORLEVEL% EQU 0 (
    echo ✓ COMPILACION EXITOSA
) else (
    echo ✗ ERROR DE COMPILACION
)
