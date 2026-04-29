@echo off
echo ========================================
echo COMPILAR Y EJECUTAR SLSKDOWN (WINFORMS)
echo ========================================
echo.

cd /d c:\p2p

echo [1/3] Limpiando compilacion anterior...
dotnet clean SlskDown\SlskDown.csproj -c Release
echo.

echo [2/3] Compilando y publicando SlskDown (Release)...
dotnet publish SlskDown\SlskDown.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR DE COMPILACION
    echo.
    pause
    exit /b 1
)

echo.
echo Compilacion exitosa
echo.

echo [3/3] Ejecutando SlskDown...
echo ========================================
cd SlskDown\bin\Release\net9.0-windows\publish
start "" SlskDown.exe

echo.
echo SlskDown iniciado
echo Ubicacion: c:\p2p\SlskDown\bin\Release\net9.0-windows\publish\SlskDown.exe
echo.
echo Esperando 3 segundos para verificar logs...
timeout /t 3 /nobreak >nul

echo.
echo Archivos de log generados:
dir /b constructor_*.txt 2>nul
dir /b close_attempt_*.txt 2>nul
dir /b program_start_*.txt 2>nul

echo.
pause
