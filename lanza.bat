@echo off

cd /d c:\p2p\SlskDownAvalonia
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%i"
set "LOG=%CD%\lanza_last_run_%TS%.log"

call :RUN 2>&1
set "RC=%ERRORLEVEL%"

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
    exit /b %RC%
)

exit /b 0

:RUN
echo ========================================
echo COMPILAR Y EJECUTAR SLSKDOWN AVALONIA UI
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
echo MainWindow.axaml:
dir MainWindow.axaml
echo.
echo MainWindow.axaml line count:
powershell -NoProfile -Command "(Get-Content .\MainWindow.axaml).Count"
echo.
echo MainWindow.axaml SHA256:
certutil -hashfile MainWindow.axaml SHA256
echo ========================================
echo.

echo [1/4] Deteniendo procesos anteriores...
taskkill /F /IM SlskDownAvalonia.exe 2>nul
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
echo Limpieza completada
echo.

echo [3/4] Compilando y publicando SlskDownAvalonia (Release)...
dotnet publish SlskDownAvalonia.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACION
    echo.
    exit /b 1
)

echo Compilacion exitosa
echo.

echo [4/4] Verificando ejecutable...
if exist "bin\Release\net9.0\publish\SlskDownAvalonia.exe" (
    echo Ejecutable encontrado
    echo.
    echo Iniciando SlskDownAvalonia...
    echo ========================================
    start "" "bin\Release\net9.0\publish\SlskDownAvalonia.exe"
    echo SlskDownAvalonia iniciado
    echo.
    echo Ubicacion: c:\p2p\SlskDownAvalonia\bin\Release\net9.0\publish\SlskDownAvalonia.exe
    echo.
    timeout /t 1 >nul
) else (
    echo ERROR: No se genero el ejecutable
    echo.
    echo Verificando errores...
    dir bin\Release\net9.0\publish\ 2>nul
    echo.
    exit /b 1
)

exit /b 0


exit /b 0
