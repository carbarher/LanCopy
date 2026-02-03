@echo off
echo Compilando SlskDown.csproj...
echo.
dotnet build SlskDown.csproj -v minimal
echo.
if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo Compilacion exitosa!
    echo ========================================
) else (
    echo ========================================
    echo Error en la compilacion
    echo ========================================
)
echo.
pause
