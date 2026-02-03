@echo off
echo Compilando SlskDown... > compile_log.txt
dotnet build SlskDown.csproj -c Release --no-incremental >> compile_log.txt 2>&1
set BUILD_RESULT=%ERRORLEVEL%

if %BUILD_RESULT% EQU 0 (
    echo.
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo.
    echo ========================================
    echo ERROR DE COMPILACION
    echo ========================================
    echo.
    echo Mostrando ultimos errores:
    echo.
    findstr /C:"error CS" compile_log.txt | findstr /N "^" | findstr "^[1-9]:" | findstr "^[1-5]:"
)
echo.
echo ========================================
echo CODIGO DE SALIDA: %BUILD_RESULT%
echo ========================================
pause
