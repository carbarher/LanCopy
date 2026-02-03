@echo off
cd /d c:\p2p\SlskDown
echo === COMPILANDO SLSKDOWN ===
dotnet build SlskDown.csproj -c Release -v normal
echo.
echo === VERIFICANDO EJECUTABLE ===
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [OK] Ejecutable generado
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo [ERROR] No se genero ejecutable
    echo Contenido de bin\Release\net8.0-windows:
    dir "bin\Release\net8.0-windows"
)
pause
