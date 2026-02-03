@echo off
echo ========================================
echo COMPILACION MANUAL - SlskDown
echo ========================================
echo.
echo Este script fuerza una recompilacion completa
echo limpiando TODO el cache del compilador.
echo.
pause

echo [1/7] Limpiando cache de NuGet...
dotnet nuget locals all --clear

echo [2/7] Deteniendo build server...
dotnet build-server shutdown

echo [3/7] Eliminando directorios de cache...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs

echo [4/7] Eliminando archivos compilados...
del /s /q *.dll 2>nul
del /s /q *.pdb 2>nul

echo [5/7] Tocando archivo SearchResultsDataSource.cs...
copy /b UI\SearchResultsDataSource.cs +,, >nul

echo [6/7] Restaurando dependencias sin cache...
dotnet restore SlskDown.csproj --force --no-cache

echo [7/7] Compilando proyecto...
dotnet build SlskDown.csproj --configuration Release --no-incremental --force /p:UseSharedCompilation=false

echo.
echo ========================================
echo VERIFICANDO RESULTADO
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ✅ COMPILACION EXITOSA
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    echo.
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo ========================================
    echo EJECUTAR APLICACION
    echo ========================================
    echo.
    set /p RUN="¿Ejecutar SlskDown ahora? (S/N): "
    if /i "%RUN%"=="S" (
        cd bin\Release\net8.0-windows
        start SlskDown.exe
        cd ..\..\..
    )
) else (
    echo.
    echo ❌ COMPILACION FALLIDA
    echo.
    echo El ejecutable no se genero.
    echo Revisa los errores arriba.
)
echo.
echo ========================================
pause
