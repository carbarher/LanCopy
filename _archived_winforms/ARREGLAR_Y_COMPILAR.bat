@echo off
cls
echo ========================================
echo ARREGLAR Y COMPILAR
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/3] Arreglando backup del 18/11...
python fix_backup_18nov.py

echo.
echo [2/3] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1

echo.
echo [3/3] Compilando...
dotnet build SlskDown.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   EXITO!
    echo ========================================
    echo.
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
        dir "bin\Release\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
    )
) else (
    echo.
    echo ERROR - Ver errores arriba
)

echo.
pause
