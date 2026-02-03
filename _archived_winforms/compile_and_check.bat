@echo off
echo ========================================
echo COMPILANDO SLSKDOWN
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/4] Matando proceso...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/4] Limpiando cache...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo [3/4] Compilando...
dotnet build SlskDown.csproj -c Release

echo.
echo [4/4] Verificando...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ========================================
    echo ✅ COMPILACION EXITOSA
    echo ========================================
    echo.
    echo Ejecutable: bin\Release\net8.0-windows\SlskDown.exe
    for %%F in (bin\Release\net8.0-windows\SlskDown.exe) do echo Tamaño: %%~zF bytes
    for %%F in (bin\Release\net8.0-windows\SlskDown.exe) do echo Fecha: %%~tF
    echo.
    echo Presiona cualquier tecla para ejecutar...
    pause >nul
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ========================================
    echo ❌ ERROR: No se generó el ejecutable
    echo ========================================
    pause
)
