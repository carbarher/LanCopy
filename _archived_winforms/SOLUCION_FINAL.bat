@echo off
cls
echo ========================================
echo SOLUCION FINAL - COMPILACION SLSKDOWN
echo ========================================
echo.
echo El problema: MainForm.cs compila bien solo,
echo pero falla cuando se compila con otros archivos.
echo.
echo SOLUCION: Usar proyecto minimal que solo compila
echo los 3 archivos esenciales.
echo.
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo Paso 1: Limpiando cache...
rmdir /s /q obj >nul 2>&1
rmdir /s /q bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo   OK

echo.
echo Paso 2: Compilando con proyecto MINIMAL...
echo   (Solo MainForm.cs + Program.cs + MainForm.Designer.cs)
echo.

dotnet build SlskDown_MINIMAL.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA!
    echo ========================================
    echo.
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo Ejecutable generado:
        echo   bin\Release\net8.0-windows\SlskDown.exe
        echo.
        echo Tamaño:
        dir "bin\Release\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
        echo.
        echo ========================================
        echo LISTO PARA EJECUTAR
        echo ========================================
        echo.
        set /p EJECUTAR="Deseas ejecutar SlskDown ahora? (S/N): "
        if /i "%EJECUTAR%"=="S" (
            start "" "bin\Release\net8.0-windows\SlskDown.exe"
            echo.
            echo SlskDown iniciado!
        )
    ) else (
        echo ERROR: No se encontro el ejecutable
    )
) else (
    echo.
    echo ========================================
    echo   ERROR DE COMPILACION
    echo ========================================
    echo.
    echo El proyecto minimal tambien falla.
    echo Esto significa que MainForm.cs tiene un problema.
    echo.
    echo SIGUIENTE PASO: Usar backup mas antiguo
    echo   MainForm.cs.backup_20251118_153012
)

echo.
pause
