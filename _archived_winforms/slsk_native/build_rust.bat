@echo off
echo Compilando Rust DLL...
cargo build --release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Compilacion exitosa
    echo.
    dir target\release\*.dll
) else (
    echo.
    echo ❌ Error en compilacion
)
pause
