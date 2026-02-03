@echo off
cls
echo ========================================
echo COMPILACION MINIMAL CON BACKUP_FULL
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/4] Verificando backup_full...
python find_brace.py
echo.

echo [2/4] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo OK

echo.
echo [3/4] Compilando SOLO MainForm.cs (sin otros archivos)...
dotnet build SlskDown_MINIMAL.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA!
    echo ========================================
    echo.
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo Ejecutable: bin\Debug\net8.0-windows\SlskDown.exe
        dir "bin\Debug\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
        echo.
        echo [4/4] LISTO PARA EJECUTAR
    )
) else (
    echo.
    echo ========================================
    echo   COMPILACION FALLO
    echo ========================================
    echo.
    echo Incluso MainForm.cs solo no compila.
    echo El archivo tiene un problema interno.
)

echo.
pause
