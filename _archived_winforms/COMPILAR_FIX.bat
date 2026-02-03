@echo off
echo ========================================
echo COMPILANDO SLSKDOWN - FIX BLOQUEO
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/4] Limpiando directorios...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo [2/4] Restaurando paquetes...
dotnet restore

echo [3/4] Compilando proyecto...
dotnet build SlskDown.csproj -c Release

echo [4/4] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo ========================================
    echo ✅ COMPILACION EXITOSA
    echo ========================================
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Ejecutando SlskDown...
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo.
    echo ========================================
    echo ❌ ERROR: No se genero el ejecutable
    echo ========================================
    echo.
    echo Verifica los errores arriba.
)

echo.
pause
