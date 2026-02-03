@echo off
echo ========================================
echo COMPILACION FINAL - VERIFICACION
echo ========================================
echo.

dotnet build SlskDown.csproj --configuration Release --no-incremental

echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo EXITO: Ejecutable generado
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ERROR: Ejecutable NO generado
)
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ==========================================
    echo   COMPILACION EXITOSA
    echo ==========================================
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo.
    echo ==========================================
    echo   ERROR EN COMPILACION
    echo ==========================================
)
pause
