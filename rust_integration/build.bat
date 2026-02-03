@echo off
echo Building SlskDown Rust components...
echo.

REM Check if Rust is installed
where cargo >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Rust is not installed!
    echo Please install Rust from https://rustup.rs/
    exit /b 1
)

echo [1/3] Building release version...
cargo build --release

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    exit /b 1
)

echo.
echo [2/3] Copying DLL to SlskDown bin directory...
copy /Y target\release\slskdown_core.dll ..\SlskDown\bin\Release\net8.0-windows\

echo.
echo [3/3] Running tests...
cargo test

echo.
echo ========================================
echo Build completed successfully!
echo DLL location: target\release\slskdown_core.dll
echo ========================================
