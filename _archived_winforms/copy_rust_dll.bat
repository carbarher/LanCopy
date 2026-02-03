@echo off
echo ========================================
echo Copiando DLL de Rust a bin
echo ========================================

if exist slsk_native\target\release\slsk_native.dll (
    echo Copiando slsk_native.dll...
    copy /Y slsk_native\target\release\slsk_native.dll bin\Debug\net8.0-windows\ 2>nul
    copy /Y slsk_native\target\release\slsk_native.dll bin\Release\net8.0-windows\ 2>nul
    echo.
    echo ========================================
    echo DLL copiada exitosamente!
    echo ========================================
    dir bin\Release\net8.0-windows\slsk_native.dll
) else (
    echo.
    echo ERROR: No se encontro slsk_native.dll
    echo Ejecuta primero: cd slsk_native ^&^& cargo build --release
    pause
    exit /b 1
)

echo.
pause
