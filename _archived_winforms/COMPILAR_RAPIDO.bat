@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release --no-incremental
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo EXITO - Ejecutando...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ERROR
    pause
)
