@echo off
echo ========================================
echo COMPILACION LIMPIA DESPUES DE REINICIO
echo ========================================
echo.

echo [1/4] Limpiando carpetas de compilacion...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs
echo OK - Carpetas eliminadas

echo.
echo [2/4] Limpiando cache de NuGet...
dotnet nuget locals all --clear
echo OK - Cache de NuGet limpiado

echo.
echo [3/4] Restaurando paquetes NuGet...
dotnet restore SlskDown.csproj
echo OK - Paquetes restaurados

echo.
echo [4/4] Compilando proyecto (sin cache incremental)...
dotnet build SlskDown.csproj --no-incremental /p:UseSharedCompilation=false > compile_post_reboot.txt 2>&1

echo.
echo ========================================
echo COMPILACION COMPLETADA
echo ========================================
echo.
echo Revisa el archivo compile_post_reboot.txt para ver los resultados
echo.

type compile_post_reboot.txt | findstr /C:"Build succeeded" /C:"Build FAILED" /C:"error"

echo.
echo Presiona cualquier tecla para ver el log completo...
pause > nul
type compile_post_reboot.txt

echo.
pause
