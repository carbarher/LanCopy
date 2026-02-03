@echo off
echo ========================================
echo COMPILACION RAPIDA (sin limpiar bin/obj)
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo Compilando SlskDown...
dotnet build SlskDown.csproj -c Release --no-incremental

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACION
    pause
    exit /b 1
)

echo.
echo ========================================
echo COMPILACION EXITOSA
echo ========================================
echo.
echo Ejecutable: bin\Release\net9.0-windows\SlskDown.exe
echo.
echo Iniciando aplicacion...
start "" "%CD%\bin\Release\net9.0-windows\SlskDown.exe"
echo.
echo Aplicacion iniciada
echo.
