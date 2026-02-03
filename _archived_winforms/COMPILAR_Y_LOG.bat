@echo off
echo Compilando y guardando log...

dotnet clean SlskDown.csproj > compilacion_log.txt 2>&1
echo Limpieza completada >> compilacion_log.txt
echo. >> compilacion_log.txt

dotnet build SlskDown.csproj -c Release -v n >> compilacion_log.txt 2>&1

echo. >> compilacion_log.txt
echo ======================================== >> compilacion_log.txt
echo RESULTADO: >> compilacion_log.txt

if exist "bin\Release\net8.0-windows\SlskDown_NEW.exe" (
    echo EXITO - Ejecutable generado >> compilacion_log.txt
    echo Ejecutando aplicacion...
    start bin\Release\net8.0-windows\SlskDown_NEW.exe
) else (
    echo ERROR - No se genero ejecutable >> compilacion_log.txt
    echo Intentando dotnet run...
    start dotnet run --project SlskDown.csproj
)

echo.
echo Log guardado en: compilacion_log.txt
echo Abriendo log...
notepad compilacion_log.txt
