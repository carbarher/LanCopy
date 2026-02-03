@echo off
echo Compilando SlskDown...
dotnet build SlskDown.csproj --configuration Release --no-incremental > build_output.txt 2>&1
echo.
echo ========================================
echo RESULTADO DE COMPILACION:
echo ========================================
type build_output.txt | findstr /C:"Build succeeded" /C:"Build FAILED" /C:"error CS" /C:"Error(s)" /C:"Warning(s)"
echo.
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo EJECUTABLE GENERADO: SI
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo EJECUTABLE GENERADO: NO
    echo.
    echo Mostrando ultimos errores:
    type build_output.txt | findstr /C:"error CS"
)
echo ========================================
pause
