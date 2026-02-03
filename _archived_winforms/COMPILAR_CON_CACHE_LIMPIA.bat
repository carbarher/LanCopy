@echo off
echo ========================================
echo COMPILACION CON CACHE LIMPIA
echo ========================================
echo.
echo Este script limpia TODA la cache de MSBuild
echo y compila desde cero.
echo.
echo Correcciones aplicadas:
echo - NicotinePlusOptimizations.cs agregado
echo - DownloadOptimizations.cs agregado
echo - Timer corregido a System.Threading.Timer
echo - Metodos agregados a IndirectConnectionManager
echo - Metodo RecordFailure agregado a DownloadRetryManager
echo - Propiedades agregadas a DownloadTask
echo - MainForm.CalibreStubs.cs creado
echo.
pause

cd /d "c:\p2p\SlskDown"

echo.
echo [1/7] Matando procesos...
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM MSBuild.exe 2>nul
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo [2/7] Eliminando carpetas bin y obj...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj

echo.
echo [3/7] Limpiando cache de NuGet...
dotnet nuget locals all --clear

echo.
echo [4/7] Apagando servidor de compilacion...
dotnet build-server shutdown
timeout /t 3 /nobreak >nul

echo.
echo [5/7] Limpiando proyecto...
dotnet clean SlskDown.csproj

echo.
echo [6/7] Restaurando paquetes...
dotnet restore SlskDown.csproj --force --no-cache

echo.
echo [7/7] Compilando sin cache...
dotnet build SlskDown.csproj -c Release --no-incremental --force /p:UseSharedCompilation=false

echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ===== COMPILACION EXITOSA =====
    echo.
    echo Ejecutable: bin\Release\net9.0-windows\SlskDown.exe
    echo.
    dir "bin\Release\net9.0-windows\SlskDown.exe"
    echo.
) else (
    echo.
    echo ===== ERROR DE COMPILACION =====
    echo Codigo de salida: %ERRORLEVEL%
    echo.
    echo Si siguen habiendo errores, verifica que los archivos
    echo tengan los cambios ejecutando:
    echo.
    echo findstr /C:"System.Threading.Timer" DownloadOptimizations.cs
    echo findstr /C:"RequestIndirectConnection" NicotinePlusOptimizations.cs
    echo findstr /C:"RemotePath =>" Models\DownloadModels.cs
    echo.
)
echo ========================================
pause
