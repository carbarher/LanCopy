@echo off
echo ========================================
echo Compilando SlskDown SIMPLE
echo ========================================
echo.

echo [1/3] Limpiando directorios...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo OK - Directorios limpiados
echo.

echo [2/3] Compilando proyecto...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR - Compilacion fallida
    pause
    exit /b 1
)
echo OK - Compilacion exitosa
echo.

echo [3/3] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo OK - Ejecutable generado
    echo.
    echo ========================================
    echo COMPILACION COMPLETADA
    echo ========================================
    echo.
    echo Ejecutando aplicacion...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ERROR - Ejecutable no encontrado
    pause
)
