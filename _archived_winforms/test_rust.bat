@echo off
echo Verificando Rust...
cargo --version
if errorlevel 1 (
    echo ERROR: Rust no instalado
    echo Instalar desde: https://rustup.rs/
    exit /b 1
)

echo.
echo Compilando Rust core...
cd rust_core
cargo build --release
if errorlevel 1 (
    echo ERROR: Fallo al compilar
    exit /b 1
)

echo.
echo Verificando DLL...
if exist target\release\slskdown_core.dll (
    echo OK: DLL generada
    dir target\release\slskdown_core.dll | find "slskdown_core.dll"
) else (
    echo ERROR: DLL no encontrada
    exit /b 1
)

cd ..
echo.
echo ============================================
echo   RUST CORE COMPILADO CORRECTAMENTE
echo ============================================
