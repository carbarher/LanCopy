@echo off
echo === Diagnostico de Rust ===
echo.

echo 1. Verificando instalacion de Rust...
rustc --version
cargo --version
echo.

echo 2. Verificando target instalado...
rustup show
echo.

echo 3. Verificando linker...
where link.exe
echo.

echo 4. Intentando compilar con target explicito...
cargo build --release --target x86_64-pc-windows-msvc
echo.

echo 5. Verificando archivos generados...
if exist "target\x86_64-pc-windows-msvc\release\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\x86_64-pc-windows-msvc\release\
    dir "target\x86_64-pc-windows-msvc\release\slskdown_core.dll"
) else (
    echo [ERROR] DLL NO encontrada
)

if exist "target\release\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\release\
    dir "target\release\slskdown_core.dll"
) else (
    echo [ERROR] DLL NO encontrada en target\release\
)

echo.
echo === Archivos .dll en todo el directorio target ===
dir /s /b target\*.dll

echo.
echo === Archivos slskdown_core.* ===
dir /s /b target\slskdown_core.*

pause
