@echo off

cd /d c:\p2p\SlskDown
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%i"
set "LOG=%CD%\lanza_last_run_%TS%.log"
set "ERRLOG=%CD%\compile_errors_latest.txt"

echo ======================================== > "%LOG%"
echo lanza.bat run: %date% %time% >> "%LOG%"
echo ======================================== >> "%LOG%"

call :RUN
set "RC=%ERRORLEVEL%"

type "%LOG%"

echo.
echo ========================================
echo CODIGO DE SALIDA: %RC%
echo ========================================

if exist "%CD%\error_log.txt" (
    echo.
    echo ========================================
    echo ERROR_LOG.TXT
    echo ========================================
    type "%CD%\error_log.txt"
    echo.
)

if not "%RC%"=="0" (
    pause
    exit /b %RC%
)

exit /b 0

:RUN
echo ========================================
echo COMPILAR Y EJECUTAR SLSKDOWN
echo ========================================
echo.

echo ========================================
echo DIAGNOSTICO ENTORNO
echo ========================================
echo Carpeta actual: %CD%
echo.
echo Dotnet path:
where dotnet
echo.
echo Dotnet version:
dotnet --version
echo.
echo MainForm.cs:
dir MainForm.cs
echo.
echo MainForm.cs line count:
powershell -NoProfile -Command "(Get-Content .\MainForm.cs).Count"
echo.
echo MainForm.cs SHA256:
certutil -hashfile MainForm.cs SHA256
echo ========================================
echo.

echo [1/4] Deteniendo procesos anteriores...
taskkill /F /IM SlskDown.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Proceso anterior detenido
    timeout /t 1 /nobreak >nul
) else (
    echo No hay procesos anteriores
)
echo.

echo [2/4] Limpiando compilacion anterior...
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul
dotnet clean SlskDown.csproj 2>nul
echo Limpieza completada
echo.

echo [3/4] Compilando SlskDown (Release - SIN CACHE)...
dotnet build SlskDown.csproj -c Release --no-incremental --force

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACION
    echo.
    exit /b 1
)

echo Compilacion exitosa
echo.

echo [4/4] Verificando ejecutable...
set "EXE_PATH=bin\Release\net9.0-windows\SlskDown.exe"
if exist "%EXE_PATH%" (
    echo Ejecutable encontrado
    echo.
    echo Iniciando SlskDown...
    echo ========================================
    start "" "%CD%\bin\Release\net9.0-windows\SlskDown.exe"
    echo SlskDown iniciado
    echo.
    echo Ubicacion: %CD%\%EXE_PATH%
    echo.
    timeout /t 1 >nul
) else (
    echo ERROR: No se genero el ejecutable
    echo.
    echo Verificando errores en bin\Release\net9.0-windows\:
    dir bin\Release\net9.0-windows\ 2>nul
    echo.
    exit /b 1
)

exit /b 0
