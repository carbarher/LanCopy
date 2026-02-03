@echo off
echo Compilando Rust con captura de salida...
cargo build --release 2>&1
echo.
echo Exit code: %ERRORLEVEL%
pause
