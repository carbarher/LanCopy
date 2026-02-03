@echo off
echo ========================================
echo REBUILD COMPLETO Y EJECUCION
echo ========================================
echo.

cd /d c:\p2p

echo [1/4] Matando procesos SlskDown...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/4] Limpiando completamente...
rmdir /s /q SlskDown\bin 2>nul
rmdir /s /q SlskDown\obj 2>nul
echo Limpieza completa OK

echo.
echo [3/4] Compilando desde cero...
dotnet build SlskDown\SlskDown.csproj -c Release --no-incremental --force

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACION
    pause
    exit /b 1
)

echo.
echo [4/4] Ejecutando SlskDown...
echo ========================================
cd SlskDown\bin\Release\net9.0-windows
SlskDown.exe

echo.
echo Aplicacion cerrada
pause
