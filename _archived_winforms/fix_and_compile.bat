@echo off
echo ========================================
echo RESTAURAR Y COMPILAR
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/5] Respaldando archivo actual...
copy /Y MainForm.cs MainForm.cs.broken >nul 2>&1
echo OK

echo [2/5] Restaurando desde backup_full...
copy /Y MainForm.cs.backup_full MainForm.cs
if %ERRORLEVEL% NEQ 0 (
    echo ERROR al copiar backup
    pause
    exit /b 1
)

echo [3/5] Verificando llaves balanceadas...
python find_brace.py

echo.
echo [4/5] Limpiando cache de compilacion...
dotnet clean SlskDown.csproj >nul 2>&1
rmdir /s /q obj >nul 2>&1
rmdir /s /q bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo OK

echo.
echo [5/5] Compilando...
dotnet build SlskDown.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo.
    if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
        echo Ejecutable generado correctamente
        echo Ubicacion: bin\Debug\net8.0-windows\SlskDown.exe
    )
) else (
    echo.
    echo ========================================
    echo ERROR DE COMPILACION
    echo ========================================
)

echo.
pause
