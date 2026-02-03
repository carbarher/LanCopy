@echo off
echo Intentando ejecutar SlskDown.exe...
echo.

cd /d "%~dp0bin\Release\net8.0-windows"

echo Verificando que el ejecutable existe...
if not exist "SlskDown.exe" (
    echo ERROR: SlskDown.exe no existe
    pause
    exit /b 1
)

echo SlskDown.exe encontrado
echo Tamaño: 
dir SlskDown.exe | findstr SlskDown.exe
echo.

echo Verificando dependencias .NET...
dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App"
echo.

echo Intentando ejecutar directamente...
SlskDown.exe
set EXITCODE=%errorlevel%

echo.
echo Exit code: %EXITCODE%
echo.

if %EXITCODE% neq 0 (
    echo ERROR: La aplicacion termino con codigo de error %EXITCODE%
) else (
    echo La aplicacion se cerro normalmente
)

echo.
echo Verificando si hay logs...
if exist "program_start.txt" (
    echo program_start.txt encontrado:
    type program_start.txt
) else (
    echo program_start.txt NO existe
)

echo.
pause
