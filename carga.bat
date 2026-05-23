@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "WAIT_MODE=0"
if /i "%~1"=="--wait" (
    set "WAIT_MODE=1"
    shift
)

set "IMP_PROJ=%~dp0SlskDownImportBiblioteca\SlskDownImportBiblioteca.csproj"
set "IMP_OUT=%~dp0SlskDownImportBiblioteca\bin\Release\net9.0"
set "IMP_EXE=%IMP_OUT%\SlskDownImportBiblioteca.exe"

echo Compilando importador (Release)...
REM No copiar DLLs a SlskDownAvalonia: si la app principal esta abierta, bloquea SlskDownBibliotecaImport.dll y falla el build.
dotnet build "%IMP_PROJ%" -c Release --nologo /p:SkipCopyImportToolToMainApp=true
if errorlevel 1 (
    echo.
    echo Fallo al compilar. Revisa los mensajes anteriores.
    echo Si ves MSB3027 sobre SlskDownAvalonia: cierra SlskDownAvalonia o compila con carga.bat ^(omitir copia a la app principal^).
    echo Si no es eso: necesitas el SDK de .NET 9 ^(o superior compatible^) instalado.
    pause
    exit /b 1
)

if not exist "%IMP_EXE%" (
    echo.
    echo No se encontro el ejecutable:
    echo   %IMP_EXE%
    pause
    exit /b 1
)

echo.
echo Iniciando importador desde la carpeta de salida...
pushd "%IMP_OUT%"
if "%WAIT_MODE%"=="1" (
    REM Modo diagnostico: espera al WinExe y conserva ERRORLEVEL.
    start "" /wait "%IMP_EXE%" %*
) else (
    REM Por defecto no bloquear terminal; la app abre en su propia ventana.
    start "" "%IMP_EXE%" %*
)
set "RC=%ERRORLEVEL%"
popd

if not "%WAIT_MODE%"=="1" (
    if "%RC%"=="0" (
        echo Ventana lanzada sin bloquear terminal.
    ) else (
        echo No se pudo lanzar el importador. Codigo de salida: %RC%
    )
    exit /b %RC%
)

if "%RC%"=="0" exit /b 0

echo.
if "%RC%"=="2" (
    echo Ya hay otra ventana del importador abierta (solo una instancia).
    echo Cierrala y vuelve a ejecutar carga.bat
) else (
    echo Codigo de salida: %RC%
    echo Los numeros negativos grandes ^(ej. -532462766^) suelen ser una excepcion .NET no controlada, no un fallo del SDK.
    echo.
    echo Si el programa se cierra al abrir, revisa en esta carpeta:
    echo   %IMP_OUT%
    echo     - import_crash.log
    echo     - Logs\import_biblioteca.log
    echo.
    echo Para diagnosticar desde consola puedes probar:
    echo   cd /d "%IMP_OUT%"
    echo   dotnet SlskDownImportBiblioteca.dll
)
echo.
pause
exit /b %RC%
