@echo off
echo Compilando RustCore...
cd /d "%~dp0"
cargo build --release
if %ERRORLEVEL% EQU 0 (
    echo Compilacion exitosa!
    echo Copiando DLL...
    copy /Y target\release\slskdown_core.dll ..\bin\Debug\slskdown_core.dll
    copy /Y target\release\slskdown_core.dll ..\bin\Release\slskdown_core.dll
    echo DLL copiada a bin\Debug y bin\Release
) else (
    echo Error en la compilacion
)
pause
