@echo off
cls
echo ========================================
echo COMPILAR CON BACKUP ANTIGUO + FIXES
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo OK

echo.
echo Compilando...
dotnet build SlskDown.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA!
    echo ========================================
    echo.
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo Ejecutable: bin\Debug\net8.0-windows\SlskDown.exe
        dir "bin\Debug\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
    )
) else (
    echo.
    echo ERROR DE COMPILACION
)

echo.
pause
