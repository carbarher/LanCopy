@echo off
echo ========================================
echo   Verificando Setup de Rust
echo ========================================
echo.

REM Verificar Rust
echo [1/4] Verificando Rust...
where cargo >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo   [X] Rust NO instalado
    echo   [!] Instalar desde: https://rustup.rs/
    goto :error
) else (
    echo   [OK] Rust instalado
    cargo --version
)
echo.

REM Verificar archivos del proyecto
echo [2/4] Verificando archivos del proyecto...
if exist "rust_core\Cargo.toml" (
    echo   [OK] Cargo.toml encontrado
) else (
    echo   [X] Cargo.toml NO encontrado
    goto :error
)

if exist "rust_core\src\lib.rs" (
    echo   [OK] lib.rs encontrado
) else (
    echo   [X] lib.rs NO encontrado
    goto :error
)

if exist "RustCore.cs" (
    echo   [OK] RustCore.cs encontrado
) else (
    echo   [X] RustCore.cs NO encontrado
    goto :error
)
echo.

REM Compilar Rust
echo [3/4] Compilando modulo Rust...
cd rust_core
cargo build --release 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo   [X] Error al compilar
    cd ..
    goto :error
) else (
    echo   [OK] Compilacion exitosa
)
cd ..
echo.

REM Verificar DLL
echo [4/4] Verificando DLL generada...
if exist "rust_core\target\release\slskdown_core.dll" (
    echo   [OK] DLL generada correctamente
    for %%I in (rust_core\target\release\slskdown_core.dll) do (
        echo   [i] Tamano: %%~zI bytes
    )
) else (
    echo   [X] DLL NO encontrada
    goto :error
)
echo.

echo ========================================
echo   TODO LISTO! ^_^
echo ========================================
echo.
echo Siguiente paso:
echo   dotnet build
echo   dotnet run
echo.
pause
exit /b 0

:error
echo.
echo ========================================
echo   SETUP INCOMPLETO
echo ========================================
echo.
echo Revisar: RUST_INTEGRATION.md
echo.
pause
exit /b 1
