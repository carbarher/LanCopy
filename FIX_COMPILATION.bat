@echo off
echo ========================================
echo SCRIPT DE CORRECCION DE COMPILACION
echo ========================================
echo.

echo [1/4] Eliminando archivo duplicado...
del /F /Q "c:\p2p\SlskDownAvalonia\Services\ObjectPoolService.cs"
if exist "c:\p2p\SlskDownAvalonia\Services\ObjectPoolService.cs" (
    echo ERROR: No se pudo eliminar el archivo
    pause
    exit /b 1
) else (
    echo OK: Archivo eliminado
)
echo.

echo [2/4] Limpiando compilacion anterior...
cd /d "c:\p2p\SlskDownAvalonia"
rmdir /S /Q bin obj 2>nul
echo OK: Limpieza completada
echo.

echo [3/4] Compilando proyecto...
dotnet build SlskDownAvalonia.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Compilacion fallida
    pause
    exit /b 1
)
echo.

echo [4/4] Verificando ejecutable...
if exist "bin\Release\net9.0\SlskDownAvalonia.exe" (
    echo.
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo Ejecutable: bin\Release\net9.0\SlskDownAvalonia.exe
    echo.
) else (
    echo.
    echo ERROR: No se genero el ejecutable
    pause
    exit /b 1
)

pause
