@echo off
echo ========================================
echo COMPILACION FORZADA - LIMPIEZA TOTAL
echo ========================================

echo.
echo [1/5] Matando procesos MSBuild y dotnet...
taskkill /F /IM MSBuild.exe 2>nul
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo [2/5] Eliminando carpetas bin y obj...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj
if exist Core\bin rmdir /S /Q Core\bin
if exist Core\obj rmdir /S /Q Core\obj

echo.
echo [3/5] Limpiando cache de NuGet...
dotnet nuget locals all --clear

echo.
echo [4/5] Restaurando paquetes...
dotnet restore --force --no-cache

echo.
echo [5/5] Compilando con limpieza total...
dotnet build -c Release --no-incremental --force /p:UseSharedCompilation=false

echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo COMPILACION EXITOSA
    echo Ejecutable: bin\Release\net9.0-windows\SlskDown.exe
) else (
    echo ERROR DE COMPILACION - Codigo: %ERRORLEVEL%
)
echo ========================================
pause
