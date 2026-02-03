@echo off
echo ========================================
echo   COMPILANDO SLSKDOWN
echo ========================================
cd /d c:\p2p\SlskDown

echo Limpiando...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release 2>&1

echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ========================================
    echo   COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutando SlskDown...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo SlskDown iniciado!
) else (
    echo ========================================
    echo   ERROR: NO SE CREO EL EJECUTABLE
    echo ========================================
    echo.
    echo Verificando archivos generados...
    if exist obj\Release\net8.0-windows dir obj\Release\net8.0-windows
)

pause
