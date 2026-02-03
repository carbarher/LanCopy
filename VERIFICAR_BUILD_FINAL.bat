@echo off
cls
echo ========================================
echo VERIFICACION FINAL DE COMPILACION
echo ========================================
echo.
cd c:\p2p\SlskDown
echo Limpiando proyecto...
dotnet clean >nul 2>&1
echo.
echo Compilando proyecto...
dotnet build SlskDown.csproj -c Release --no-incremental
echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo RESULTADO: COMPILACION EXITOSA
    echo Estado: OK - 0 errores
) else (
    echo RESULTADO: COMPILACION FALLIDA
    echo Estado: ERROR - Revisar logs arriba
)
echo ========================================
pause
