@echo off
echo Probando compilacion Rust...
echo.

REM Probar MSVC
echo [1] Intentando con MSVC (default)...
cargo clean >nul 2>&1
cargo build --release 2>&1 | findstr /i "compiling finished error linking"
echo.

REM Verificar DLL MSVC
if exist "target\release\slskdown_core.dll" (
    echo *** EXITO MSVC: DLL generada ***
    dir target\release\slskdown_core.dll
    exit /b 0
)

REM Probar GNU
echo [2] Intentando con GNU toolchain...
cargo clean >nul 2>&1
cargo build --release --target x86_64-pc-windows-gnu 2>&1 | findstr /i "compiling finished error linking"
echo.

REM Verificar DLL GNU
if exist "target\x86_64-pc-windows-gnu\release\slskdown_core.dll" (
    echo *** EXITO GNU: DLL generada ***
    dir target\x86_64-pc-windows-gnu\release\slskdown_core.dll
    exit /b 0
)

echo.
echo *** AMBOS TOOLCHAINS FALLARON ***
echo.
echo Verificando archivos generados:
dir /s /b target\*.dll 2>nul | findstr /v "async_trait rustversion serde_derive thiserror"

pause
