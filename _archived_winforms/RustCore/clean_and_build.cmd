@echo off
echo Limpiando archivos de compilacion anteriores...
cargo clean
echo.
echo Esperando 2 segundos...
timeout /t 2 /nobreak >nul
echo.
echo Compilando en modo release...
cargo build --release
echo.
if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo Compilacion exitosa!
    echo ========================================
    echo.
    echo Copiando DLL a directorios de salida...
    if not exist "..\bin\Debug" mkdir "..\bin\Debug"
    if not exist "..\bin\Release" mkdir "..\bin\Release"
    copy /Y target\release\slskdown_core.dll ..\bin\Debug\slskdown_core.dll
    copy /Y target\release\slskdown_core.dll ..\bin\Release\slskdown_core.dll
    echo.
    echo DLL copiada exitosamente!
    echo Ubicaciones:
    echo   - bin\Debug\slskdown_core.dll
    echo   - bin\Release\slskdown_core.dll
) else (
    echo ========================================
    echo Error en la compilacion
    echo ========================================
)
echo.
pause
