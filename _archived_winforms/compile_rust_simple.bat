@echo off
echo =====================================
echo   COMPILANDO RUST (13 funcionalidades)
echo =====================================
echo.

cd rust_core

echo [1/2] Compilando...
cargo build --release
if errorlevel 1 (
    echo.
    echo ERROR: Compilacion fallida
    pause
    exit /b 1
)

echo.
echo [2/2] Copiando DLL...
copy /Y target\release\slskdown_core.dll ..\slskdown_core.dll
if errorlevel 1 (
    echo ERROR: No se pudo copiar la DLL
    pause
    exit /b 1
)

cd ..

echo.
echo =====================================
echo   COMPILACION EXITOSA
echo =====================================
echo.
echo DLL: %CD%\slskdown_core.dll
dir slskdown_core.dll
echo.

pause
