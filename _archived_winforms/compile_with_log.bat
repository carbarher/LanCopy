@echo off
cd /d c:\p2p\SlskDown
echo Compilando con log detallado...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release /bl:build.binlog /flp:logfile=build_errors.log;errorsonly
echo.
echo Mostrando errores...
if exist build_errors.log (
    type build_errors.log
) else (
    echo No se genero archivo de errores
)
echo.
pause
