@echo off
echo Cerrando todos los procesos SlskDown...
tasklist | findstr /i "SlskDown.exe" >nul
if %ERRORLEVEL% EQU 0 (
    for /f "tokens=2" %%a in ('tasklist ^| findstr /i "SlskDown.exe"') do (
        taskkill /F /PID %%a 2>nul
    )
)
timeout /t 2 /nobreak >nul

echo Limpiando archivos...
del /f /q startup_log.txt error_log.txt fatal_error.txt 2>nul

echo Compilando...
dotnet build SlskDown.csproj -c Release --nologo --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo ERROR DE COMPILACION
    pause
    exit /b 1
)

echo Ejecutando aplicacion...
start /B bin\Release\net8.0-windows\SlskDown.exe

echo Esperando 5 segundos...
timeout /t 5 /nobreak >nul

echo.
echo === CONTENIDO DE startup_log.txt ===
type startup_log.txt 2>nul
echo.
echo === FIN DEL LOG ===
echo.

echo Cerrando proceso...
taskkill /F /IM SlskDown.exe 2>nul

pause
