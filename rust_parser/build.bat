@echo off
echo Compilando parser Rust...
cargo build --release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Compilación exitosa
    echo DLL: target\release\emule_html_parser.dll
    echo.
    echo Para usar en C#, copia el DLL a la carpeta del ejecutable
) else (
    echo.
    echo ❌ Error en compilación
)

pause
