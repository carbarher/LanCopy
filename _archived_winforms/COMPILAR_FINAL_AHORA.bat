@echo off
cls
echo ========================================
echo COMPILACION FINAL
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo Limpiando...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1

echo.
echo Compilando con proyecto actualizado...
dotnet build SlskDown_MINIMAL.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   EXITO!
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
    echo ERROR DE COMPILACION
)

echo.
pause
