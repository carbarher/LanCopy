@echo off
echo Limpiando...
dotnet clean SlskDown.csproj > nul 2>&1
echo Compilando...
dotnet build SlskDown.csproj -c Release 2>&1
echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo COMPILACION EXITOSA
) else (
    echo COMPILACION FALLIDA
)
pause
