@echo off
echo ========================================
echo Compilando con DEBUG
echo ========================================
echo.
dotnet build SlskDown.csproj -c Debug --verbosity normal > build_output.txt 2>&1
echo.
echo SALIDA COMPLETA:
echo ========================================
type build_output.txt
echo.
echo ========================================
if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
    echo [EXITO] Ejecutable generado en Debug
    dir bin\Debug\net8.0-windows\SlskDown.exe
) else (
    echo [ERROR] No se genero ejecutable
)
echo ========================================
pause
