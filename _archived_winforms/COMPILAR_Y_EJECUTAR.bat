@echo off
cls
echo ========================================
echo 🚀 COMPILAR Y EJECUTAR SLSKDOWN
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/4] Limpiando compilación anterior...
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul
echo ✅ Limpieza completada

echo.
echo [2/4] Compilando SlskDown (Debug)...
dotnet build SlskDown.csproj -c Debug --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR DE COMPILACIÓN
    echo.
    echo Intentando con verbosidad detallada...
    dotnet build SlskDown.csproj -c Debug
    pause
    exit /b 1
)

echo ✅ Compilación exitosa

echo.
echo [3/4] Compilando SlskDown (Release)...
dotnet build SlskDown.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ ERROR DE COMPILACIÓN
    pause
    exit /b 1
)
echo ✅ Compilación exitosa

echo.
echo [4/4] Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ Ejecutable encontrado
    echo.
    echo 🚀 Iniciando SlskDown...
    echo ========================================
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
    echo ✅ SlskDown iniciado correctamente!
    echo.
    echo 📂 Ubicación: c:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo ⏹️ Presiona cualquier tecla para cerrar esta ventana...
    pause >nul
) else (
    echo ❌ ERROR: No se generó el ejecutable
    echo.
    echo Verificando errores...
    dir bin\Release\net8.0-windows\ 2>nul
    echo.
    pause
)
