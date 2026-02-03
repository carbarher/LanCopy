@echo off
echo Compilando SlskDown...
@echo off
echo Compilando...
dotnet build -c Release 2>&1 | findstr /C:"error CS" /C:"Build succeeded" /C:"Build FAILED" /C:"Errores"
echo.
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo EXITO
) else (
    echo FALLO
)
pause --no-incremental
echo.
echo Presiona cualquier tecla...
pause >nul
