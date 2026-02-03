@echo off
echo ========================================
echo   EJECUTANDO VERSION NUEVA
echo ========================================

echo Cerrando versiones anteriores...
taskkill /F /IM SlskDown.exe 2>nul

echo.
echo Limpiando compilacion anterior...
cd /d c:\p2p\SlskDown
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo Compilando version nueva...
dotnet build SlskDown.csproj -c Release

echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ========================================
    echo   COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ubicacion del ejecutable:
    echo %CD%\bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Fecha de compilacion:
    dir /T:W "bin\Release\net8.0-windows\SlskDown.exe" | find "SlskDown.exe"
    echo.
    echo Ejecutando VERSION NUEVA...
    echo ========================================
    start "" "%CD%\bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo VERSION NUEVA EJECUTADA!
    echo.
) else (
    echo ========================================
    echo   ERROR: NO SE CREO EL EJECUTABLE
    echo ========================================
)

pause
