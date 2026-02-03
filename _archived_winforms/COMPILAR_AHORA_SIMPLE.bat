@echo off
cd /d "C:\p2p\SlskDown"

echo Matando procesos...
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM MSBuild.exe 2>nul
timeout /t 1 /nobreak >nul

echo Limpiando...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj

echo Apagando servidor de compilacion...
dotnet build-server shutdown
timeout /t 2 /nobreak >nul

echo Compilando con dotnet...
"C:\Program Files\dotnet\dotnet.exe" build SlskDown.csproj -c Release

echo.
echo Verificando resultado...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ===== EXITO =====
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Ejecutando...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ===== ERROR: No se genero ejecutable =====
    echo.
    echo Intentando con ruta completa...
    "C:\Program Files\dotnet\dotnet.exe" --version
    echo.
    "C:\Program Files\dotnet\dotnet.exe" --list-sdks
)
pause
