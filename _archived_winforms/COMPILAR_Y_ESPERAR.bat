@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo LIMPIANDO Y COMPILANDO SLSKDOWN
echo ========================================
echo.

echo [1/4] Eliminando cache...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo [2/4] Limpiando proyecto...
"C:\Program Files\dotnet\dotnet.exe" clean >nul 2>&1

echo [3/4] Compilando...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release

echo.
echo [4/4] Verificando resultado...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutable generado en:
    echo bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause >nul
    echo.
    echo Iniciando SlskDown...
    cd bin\Release\net8.0-windows
    start "" SlskDown.exe
    cd ..\..\..
) else (
    echo.
    echo ========================================
    echo   ERROR EN COMPILACION
    echo ========================================
    echo.
    echo Revisa los mensajes de error arriba
)
echo.
echo Presiona cualquier tecla para cerrar...
pause >nul
