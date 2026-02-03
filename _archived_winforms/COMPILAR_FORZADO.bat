@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo COMPILACION FORZADA (limpieza total)
echo ========================================
echo.

echo [1/5] Eliminando cache...
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul

echo [2/5] Limpiando proyecto...
"C:\Program Files\dotnet\dotnet.exe" clean >nul 2>&1

echo [3/5] Restaurando paquetes...
"C:\Program Files\dotnet\dotnet.exe" restore

echo [4/5] Compilando desde cero...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release --no-incremental

echo.
echo [5/5] Verificando...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause >nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ========================================
    echo   ERROR - NO SE GENERO EJECUTABLE
    echo ========================================
    echo.
    echo Revisa los errores arriba
    pause
)
