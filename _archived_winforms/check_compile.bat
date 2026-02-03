@echo off
echo Compilando y verificando errores...
echo.

dotnet build --configuration Release --no-incremental > compile_result.txt 2>&1

echo Resultado:
echo.

findstr /C:"Build succeeded" compile_result.txt > nul
if %errorlevel% == 0 (
    echo ✅ COMPILACION EXITOSA
    findstr /C:"Warning(s)" compile_result.txt
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo ❌ COMPILACION FALLIDA
    echo.
    echo Contando errores...
    findstr /C:"error CS" compile_result.txt > errors_only.txt
    for /f %%a in ('find /c /v "" ^< errors_only.txt') do set errorcount=%%a
    echo Total de errores: %errorcount%
    echo.
    echo Primeros 10 errores:
    findstr /C:"error CS" compile_result.txt | more +1 | findstr /N "^" | findstr "^[1-9]:" | findstr "^[1-9]:"
)

echo.
pause
