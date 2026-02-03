@echo off
echo ========================================
echo Compilando Rust DLL para SlskDown
echo ========================================
echo.

echo [1/5] Limpiando build anterior...
cargo clean
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: cargo clean fallo
    pause
    exit /b 1
)

echo.
echo [2/5] Verificando configuracion...
rustc --version
cargo --version
rustup show

echo.
echo [3/5] Compilando con target explicito...
cargo build --release --target x86_64-pc-windows-msvc
set BUILD_RESULT=%ERRORLEVEL%

echo.
echo [4/5] Verificando archivos generados...
if exist "target\x86_64-pc-windows-msvc\release\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\x86_64-pc-windows-msvc\release\
    dir "target\x86_64-pc-windows-msvc\release\slskdown_core.dll"
    
    echo.
    echo [5/5] Copiando DLL a directorio de salida C#...
    copy "target\x86_64-pc-windows-msvc\release\slskdown_core.dll" "..\bin\Release\net8.0-windows\" /Y
    if %ERRORLEVEL% EQU 0 (
        echo [OK] DLL copiada exitosamente
    ) else (
        echo [ERROR] No se pudo copiar la DLL
    )
) else if exist "target\release\slskdown_core.dll" (
    echo [OK] DLL encontrada en target\release\
    dir "target\release\slskdown_core.dll"
    
    echo.
    echo [5/5] Copiando DLL a directorio de salida C#...
    copy "target\release\slskdown_core.dll" "..\bin\Release\net8.0-windows\" /Y
    if %ERRORLEVEL% EQU 0 (
        echo [OK] DLL copiada exitosamente
    ) else (
        echo [ERROR] No se pudo copiar la DLL
    )
) else (
    echo [ERROR] DLL NO encontrada
    echo.
    echo Archivos .dll en target:
    dir /s /b target\*.dll 2>nul
    echo.
    echo Archivos slskdown_core en target:
    dir /s /b target\slskdown_core.* 2>nul
)

echo.
echo ========================================
echo Build result: %BUILD_RESULT%
echo ========================================
pause
