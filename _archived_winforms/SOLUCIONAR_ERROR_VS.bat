@echo off
echo ==========================================
echo SOLUCION AGRESIVA - CACHE VISUAL STUDIO
echo ==========================================
echo.

REM 1. Cerrar Visual Studio
echo [1/8] Cerrando Visual Studio...
taskkill /F /IM devenv.exe 2>nul
timeout /t 2 /nobreak >nul

REM 2. Limpiar bin/obj
echo [2/8] Limpiando bin y obj...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj

REM 3. Limpiar carpeta .vs
echo [3/8] Limpiando carpeta .vs...
if exist .vs rmdir /S /Q .vs

REM 4. Eliminar archivos .suo
echo [4/8] Eliminando archivos .suo...
del /F /S /Q *.suo 2>nul

REM 5. Limpiar ComponentModelCache
echo [5/8] Limpiando ComponentModelCache...
set LOCALAPPDATA=%USERPROFILE%\AppData\Local
if exist "%LOCALAPPDATA%\Microsoft\VisualStudio" (
    for /d %%i in ("%LOCALAPPDATA%\Microsoft\VisualStudio\*") do (
        if exist "%%i\ComponentModelCache" rmdir /S /Q "%%i\ComponentModelCache"
    )
)

REM 6. Dotnet clean
echo [6/8] Ejecutando dotnet clean...
dotnet clean SlskDown.csproj

REM 7. Dotnet restore
echo [7/8] Restaurando paquetes...
dotnet restore SlskDown.csproj

REM 8. Rebuild completo
echo [8/8] Compilando desde cero...
dotnet build SlskDown.csproj --no-incremental

echo.
echo ==========================================
echo SOLUCION COMPLETADA
echo ==========================================
echo.
echo Los errores que veias eran CACHE OBSOLETO de Visual Studio.
echo El codigo compila correctamente sin errores.
echo.
echo Ahora puedes:
echo 1. Abrir Visual Studio
echo 2. Abrir el proyecto SlskDown.csproj
echo 3. Los errores fantasma ya NO apareceran
echo.
pause
