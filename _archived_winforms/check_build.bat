@echo off
echo ========================================
echo Compilando SlskDown
echo ========================================
echo.

dotnet build --configuration Release --no-incremental > build_check.txt 2>&1

echo.
echo Resultado de la compilacion:
echo.

findstr /C:"Build succeeded" build_check.txt > nul
if %errorlevel% == 0 (
    echo ✅ COMPILACION EXITOSA
    echo.
    findstr /C:"Warning(s)" build_check.txt
    echo.
) else (
    echo ❌ COMPILACION FALLIDA
    echo.
    echo Errores encontrados:
    findstr /C:"error CS" build_check.txt
    echo.
)

echo ========================================
pause
