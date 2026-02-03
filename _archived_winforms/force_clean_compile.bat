@echo off
echo Limpieza profunda del cache del compilador...
echo.

echo [1/5] Matando procesos del compilador...
taskkill /F /IM MSBuild.exe 2>nul
taskkill /F /IM dotnet.exe 2>nul
taskkill /F /IM VBCSCompiler.exe 2>nul
timeout /t 2 /nobreak >nul

echo [2/5] Eliminando directorios de compilacion...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs

echo [3/5] Limpiando cache de NuGet...
dotnet nuget locals all --clear

echo [4/5] Esperando 3 segundos...
timeout /t 3 /nobreak >nul

echo [5/5] Compilando desde cero...
dotnet build SlskDown.csproj -c Release --no-incremental --force

echo.
echo ========================================
if %ERRORLEVEL% EQU 0 (
    echo COMPILACION EXITOSA
) else (
    echo COMPILACION FALLIDA - Codigo: %ERRORLEVEL%
)
echo ========================================
pause
