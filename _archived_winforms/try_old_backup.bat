@echo off
echo ========================================
echo PROBAR CON BACKUP ANTIGUO
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/4] Probando backup del 18/11 - 15:30...
copy /Y MainForm.cs.backup_20251118_153012 MainForm.cs >nul
python -c "with open('MainForm.cs', 'r', encoding='utf-8') as f: lines = f.readlines(); balance = 0; [balance := balance + line.count('{') - line.count('}') for line in lines]; print(f'Balance: {balance}')"
if %ERRORLEVEL% NEQ 0 (
    echo Python no disponible, probando compilacion directamente...
)

echo.
echo [2/4] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1

echo.
echo [3/4] Compilando...
dotnet build SlskDown.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo EXITO CON BACKUP DEL 18/11
    echo ========================================
    goto :success
)

echo.
echo Fallo con primer backup, probando segundo...
echo.

echo [1/4] Probando backup del 18/11 - 16:56...
copy /Y MainForm.cs.backup_20251118_165624 MainForm.cs >nul

echo [2/4] Limpiando cache...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1

echo [3/4] Compilando...
dotnet build SlskDown.csproj -c Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo EXITO CON BACKUP DEL 18/11 (16:56)
    echo ========================================
    goto :success
)

echo.
echo ========================================
echo TODOS LOS BACKUPS FALLAN
echo El problema no es el archivo MainForm.cs
echo Debe ser un archivo parcial compilandose
echo ========================================
goto :end

:success
echo.
if exist "bin\Debug\net8.0-windows\SlskDown.exe" (
    echo Ejecutable generado: bin\Debug\net8.0-windows\SlskDown.exe
)

:end
echo.
pause
