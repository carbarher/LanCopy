@echo off
cd /d c:\p2p\SlskDown
echo [PASO 1] Limpiando cache...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
echo.
echo [PASO 2] Compilando con dotnet...
dotnet build SlskDown.csproj -c Release -v normal > build_output.log 2>&1
echo.
echo [PASO 3] Mostrando resultado...
type build_output.log
echo.
echo.
echo [PASO 4] Buscando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ============================================
    echo EXITO - EJECUTABLE GENERADO
    echo ============================================
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Ejecutando...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ============================================
    echo ERROR - NO SE GENERO EJECUTABLE
    echo ============================================
    echo.
    echo Buscando archivos generados...
    dir /s /b bin\*.exe 2>nul
    dir /s /b bin\*.dll 2>nul
)
echo.
pause
