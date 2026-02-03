@echo off
echo ========================================
echo LIMPIANDO CACHE DE VISUAL STUDIO
echo ========================================
echo.

REM Cerrar Visual Studio si está abierto
echo Cerrando Visual Studio...
taskkill /F /IM devenv.exe 2>nul
timeout /t 2 /nobreak >nul

REM Limpiar bin y obj
echo Limpiando carpetas bin y obj...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj

REM Limpiar archivos temporales de VS
echo Limpiando archivos temporales...
if exist .vs rmdir /S /Q .vs

REM Limpiar solucion
echo Ejecutando dotnet clean...
dotnet clean SlskDown.csproj

REM Restaurar paquetes
echo Restaurando paquetes...
dotnet restore SlskDown.csproj

REM Compilar desde cero
echo Compilando desde cero...
dotnet build SlskDown.csproj --no-incremental

echo.
echo ========================================
echo LIMPIEZA COMPLETADA
echo ========================================
echo.
echo Ahora puedes abrir Visual Studio de nuevo
echo.
pause
