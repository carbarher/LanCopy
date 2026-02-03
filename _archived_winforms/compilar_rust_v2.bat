@echo off
echo ========================================
echo COMPILAR RUST PACK 4 V2 - WORKER POOL
echo ========================================
echo.

cd /d c:\p2p\SlskDown\rust_core

echo [1/3] Compilando Rust Pack 4 V2...
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Compilacion Rust fallo
    pause
    exit /b 1
)

echo.
echo [2/3] Copiando DLL...
echo Cerrando SlskDown si esta abierto...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

cd ..
copy /Y rust_core\target\release\slskdown_core.dll bin\Release\net9.0-windows\slskdown_core.dll
copy /Y rust_core\target\release\slskdown_core.dll bin\Release\net9.0-windows\net9.0\slskdown_core.dll

echo.
echo [3/3] Ejecutando test...
cd bin\Release\net9.0-windows\net9.0
TestRustPack4.exe

echo.
echo ========================================
echo COMPILACION COMPLETADA
echo ========================================
echo.
echo Ahora ejecuta: lanza.bat
echo.
pause
