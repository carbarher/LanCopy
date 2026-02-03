@echo off
echo ========================================
echo COMPILANDO RUST PACK 4 CORREGIDO
echo ========================================
echo.

cd /d c:\p2p\SlskDown\rust_core

echo [1/3] Compilando Rust con API serializada...
cargo build --release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR EN COMPILACION RUST
    pause
    exit /b 1
)

echo.
echo [2/3] Copiando DLL actualizada...
copy /Y target\release\slskdown_core.dll ..\bin\Release\net9.0-windows\slskdown_core.dll

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR COPIANDO DLL
    pause
    exit /b 1
)

echo.
echo [3/3] Compilando C# con Rust Pack 4 reactivado...
cd /d c:\p2p\SlskDown
dotnet build SlskDown.csproj -c Release --no-incremental

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR COMPILACION C#
    pause
    exit /b 1
)

echo.
echo ========================================
echo COMPILACION EXITOSA
echo ========================================
echo.
echo Rust Pack 4 corregido y reactivado
echo.
echo Ejecutable: bin\Release\net9.0-windows\SlskDown.exe
echo.
echo Iniciando aplicacion...
start "" "%CD%\bin\Release\net9.0-windows\SlskDown.exe"
echo.
