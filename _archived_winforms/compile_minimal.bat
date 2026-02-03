@echo off
echo ========================================
echo COMPILAR CON PROYECTO MINIMAL
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/3] Limpiando...
dotnet clean SlskDown_MINIMAL.csproj >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
echo OK

echo.
echo [2/3] Compilando solo MainForm.cs + Program.cs...
dotnet build SlskDown_MINIMAL.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [3/3] Verificando ejecutable...
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo.
        echo ========================================
        echo EXITO - COMPILACION MINIMAL
        echo ========================================
        echo.
        echo Ejecutable: bin\Debug\net8.0-windows\SlskDown.exe
        dir "bin\Debug\net8.0-windows\SlskDown.exe"
    ) else (
        echo.
        echo ERROR: No se genero el ejecutable
    )
) else (
    echo.
    echo ERROR DE COMPILACION
)

echo.
pause
