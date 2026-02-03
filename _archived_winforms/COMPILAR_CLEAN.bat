@echo off
cls
echo ========================================
echo COMPILACION LIMPIA - PROYECTO CLEAN
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo Limpiando...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1

echo.
echo Compilando con proyecto CLEAN...
echo (Solo archivos esenciales, sin dependencias problemáticas)
echo.

dotnet build SlskDown_CLEAN.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA!
    echo ========================================
    echo.
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo Ejecutable generado:
        dir "bin\Release\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
        echo.
        set /p EJECUTAR="Ejecutar SlskDown? (S/N): "
        if /i "%EJECUTAR%"=="S" (
            start "" "bin\Release\net8.0-windows\SlskDown.exe"
            echo SlskDown iniciado!
        )
    )
) else (
    echo.
    echo ========================================
    echo   ERROR DE COMPILACION
    echo ========================================
    echo.
    echo Ver errores arriba
)

echo.
pause
