@echo off
cls
echo ========================================
echo FIX FINAL - RESTAURAR Y COMPILAR
echo ========================================
echo.
echo PROBLEMA IDENTIFICADO:
echo   MainForm.cs tiene 1 llave de cierre extra
echo   en la linea 40619
echo.
echo SOLUCION:
echo   Restaurar desde backup_full que tiene
echo   llaves balanceadas (7926 de cada tipo)
echo.
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/5] Respaldando archivo actual...
copy /Y MainForm.cs MainForm.cs.broken_40619 >nul
echo   OK - Guardado como MainForm.cs.broken_40619

echo.
echo [2/5] Restaurando desde backup_full...
copy /Y MainForm.cs.backup_full MainForm.cs
if %ERRORLEVEL% NEQ 0 (
    echo   ERROR al copiar backup
    pause
    exit /b 1
)
echo   OK

echo.
echo [3/5] Verificando llaves balanceadas...
python find_brace.py
echo.

echo [4/5] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo   OK

echo.
echo [5/5] Compilando...
dotnet build SlskDown.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   COMPILACION EXITOSA!
    echo ========================================
    echo.
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo Ejecutable generado correctamente
        echo Ubicacion: bin\Debug\net8.0-windows\SlskDown.exe
        echo.
        dir "bin\Debug\net8.0-windows\SlskDown.exe" | findstr SlskDown.exe
    )
) else (
    echo.
    echo ========================================
    echo   COMPILACION FALLO
    echo ========================================
    echo.
    echo El backup_full tambien tiene problemas.
    echo Probando con backup mas antiguo...
    echo.
    copy /Y MainForm.cs.backup_20251118_153012 MainForm.cs
    dotnet clean >nul 2>&1
    rmdir /s /q obj bin >nul 2>&1
    dotnet build SlskDown.csproj -c Debug
)

echo.
pause
