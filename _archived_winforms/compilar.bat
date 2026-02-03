@echo off
echo ========================================
echo Cerrando SlskDown si esta en ejecucion...
echo ========================================
taskkill /F /IM SlskDown.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Proceso cerrado, esperando liberacion del archivo...
    timeout /t 3 /nobreak >nul
) else (
    echo No hay procesos SlskDown en ejecucion
)

echo Verificando si el archivo esta bloqueado...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    del /F "bin\Release\net8.0-windows\SlskDown.exe" 2>nul
    if %ERRORLEVEL% NEQ 0 (
        echo ADVERTENCIA: El archivo sigue bloqueado
        echo Intenta cerrar manualmente SlskDown y presiona una tecla...
        pause
    )
)

echo.
echo ========================================
echo Limpiando archivos temporales...
echo ========================================
rmdir /S /Q bin 2>nul
rmdir /S /Q obj 2>nul

echo.
echo ========================================
echo Compilando SlskDown...
echo ========================================
dotnet build SlskDown.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause >nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ========================================
    echo ERROR EN LA COMPILACION
    echo ========================================
    pause
)
