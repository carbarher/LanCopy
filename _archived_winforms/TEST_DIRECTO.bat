@echo off
echo Probando ejecutar SlskDown directamente...
echo.

cd bin\Release\net8.0-windows

echo Verificando que el archivo existe...
if not exist SlskDown.exe (
    echo ERROR: SlskDown.exe no existe
    pause
    exit /b 1
)

echo Archivo encontrado. Ejecutando...
echo.

SlskDown.exe 2>&1

echo.
echo Codigo de salida: %ERRORLEVEL%
echo.

if exist error_log.txt (
    echo === ERROR_LOG.TXT ===
    type error_log.txt
)

if exist fatal_error.txt (
    echo === FATAL_ERROR.TXT ===
    type fatal_error.txt
)

pause
