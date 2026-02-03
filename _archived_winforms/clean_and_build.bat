@echo off
echo ========================================
echo LIMPIEZA AGRESIVA DE CACHE
echo ========================================

echo Eliminando directorios de cache...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs

echo Eliminando archivos compilados...
del /s /q *.dll 2>nul
del /s /q *.pdb 2>nul
del /s /q *.cache 2>nul

echo Limpiando proyecto...
dotnet clean SlskDown.csproj --configuration Release

echo.
echo ========================================
echo RESTAURANDO DEPENDENCIAS
echo ========================================
dotnet restore SlskDown.csproj --force --no-cache

echo.
echo ========================================
echo COMPILANDO PROYECTO
echo ========================================
dotnet build SlskDown.csproj --configuration Release --no-incremental --force

echo.
echo ========================================
echo VERIFICANDO RESULTADO
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ COMPILACION EXITOSA
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ❌ COMPILACION FALLIDA
    echo Ejecutable no encontrado
)
echo ========================================
pause
