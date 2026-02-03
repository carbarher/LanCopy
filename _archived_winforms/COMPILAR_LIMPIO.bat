@echo off
cls
echo ========================================
echo LIMPIEZA Y COMPILACIÓN FORZADA
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/6] Cerrando todos los procesos SlskDown...
for /f "tokens=2" %%a in ('tasklist ^| findstr /i "SlskDown.exe"') do (
    echo Cerrando proceso %%a
    taskkill /F /PID %%a 2>nul
)
timeout /t 3 /nobreak >nul
echo Procesos cerrados

echo.
echo [2/6] Eliminando archivos temporales y compilados...
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul
if exist error_log.txt del /f /q error_log.txt 2>nul
if exist startup_log.txt del /f /q startup_log.txt 2>nul
if exist fatal_error.txt del /f /q fatal_error.txt 2>nul
echo Limpieza completada

echo.
echo [3/6] Restaurando paquetes NuGet...
dotnet restore SlskDown.csproj --verbosity minimal --nologo
if %ERRORLEVEL% NEQ 0 (
    echo ERROR al restaurar paquetes
    pause
    exit /b 1
)
echo Paquetes restaurados

echo.
echo [4/6] Limpiando caché de compilación...
dotnet clean SlskDown.csproj --verbosity minimal --nologo
echo Caché limpiado

echo.
echo [5/6] Compilando SlskDown (Release)...
dotnet build SlskDown.csproj -c Release --no-restore --verbosity minimal --nologo
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACIÓN
    echo.
    echo Intentando con verbosidad detallada...
    dotnet build SlskDown.csproj -c Release --no-restore
    pause
    exit /b 1
)
echo Compilación exitosa

echo.
echo [6/6] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo Ejecutable generado correctamente
    echo.
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Ubicación: c:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Iniciando SlskDown en 3 segundos...
    timeout /t 3 /nobreak >nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Aplicación iniciada
    echo.
    echo Revisa los archivos error_log.txt y startup_log.txt si hay problemas
) else (
    echo ERROR: No se generó el ejecutable
    pause
    exit /b 1
)

echo.
timeout /t 3 /nobreak >nul
echo ========================================
echo.
pause
