@echo off
echo ========================================
echo DIAGNOSTICO COMPLETO RUST DLL
echo ========================================
echo.

echo [1] Version de Rust:
rustc --version --verbose
echo.

echo [2] Toolchain activo:
rustup show
echo.

echo [3] Targets instalados:
rustup target list --installed
echo.

echo [4] Version de Cargo:
cargo --version
echo.

echo [5] Configuracion de Cargo.toml:
type Cargo.toml
echo.

echo [6] Contenido de lib.rs:
type src\lib.rs
echo.

echo [7] Compilacion con output verbose:
cargo build --release --verbose
echo.

echo [8] Archivos generados en target\release:
dir /s /b target\release\*.dll 2>nul
dir /s /b target\release\*.lib 2>nul
dir /s /b target\release\*.rlib 2>nul
echo.

echo [9] Archivos en target\release\deps:
dir target\release\deps\slskdown* 2>nul
echo.

echo [10] Variables de entorno relevantes:
echo RUSTUP_HOME=%RUSTUP_HOME%
echo CARGO_HOME=%CARGO_HOME%
echo PATH (Rust)=%PATH% | findstr /i rust
echo.

echo [11] Verificar linker MSVC:
where link.exe 2>nul
echo.

echo [12] Compilacion con target explicito:
cargo build --release --target x86_64-pc-windows-msvc --verbose
echo.

echo [13] Archivos generados con target explicito:
dir /s /b target\x86_64-pc-windows-msvc\release\*.dll 2>nul
echo.

echo ========================================
echo DIAGNOSTICO COMPLETADO
echo ========================================
pause
