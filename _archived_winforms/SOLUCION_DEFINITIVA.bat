@echo off
cls
echo ========================================
echo SOLUCION DEFINITIVA
echo ========================================
echo.
echo Usando backup del 18/11 (version mas compatible)
echo con proyecto original SlskDown.csproj
echo.

cd /d c:\p2p\SlskDown

echo [1/3] Restaurando backup del 18/11...
copy /Y MainForm.cs.backup_20251118_153012 MainForm.cs
echo OK

echo.
echo [2/3] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo OK

echo.
echo [3/3] Compilando con proyecto ORIGINAL...
dotnet build SlskDown.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   EXITO!
    echo ========================================
    echo.
    if exist "bin\Release\net8.0-windows\SlskDown.exe" (
        echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
        dir "bin\Release\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
        echo.
        set /p EJECUTAR="Ejecutar? (S/N): "
        if /i "%EJECUTAR%"=="S" (
            start "" "bin\Release\net8.0-windows\SlskDown.exe"
        )
    )
) else (
    echo.
    echo ERROR - Ver errores arriba
)

echo.
pause
