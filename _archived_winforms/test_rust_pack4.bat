@echo off
echo ========================================
echo TEST RUST PACK 4
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/3] Compilando Rust Pack 4...
cd rust_core
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Compilacion Rust fallo
    pause
    exit /b 1
)

echo.
echo [2/3] Copiando DLL...
echo Cerrando SlskDown si esta abierto...
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul
copy /Y target\release\slskdown_core.dll ..\bin\Release\net9.0-windows\slskdown_core.dll
copy /Y target\release\slskdown_core.dll ..\bin\Release\net9.0-windows\net9.0\slskdown_core.dll

cd ..

echo.
echo [3/3] Compilando test C#...
dotnet build TestRustPack4.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: No se pudo compilar el test
    pause
    exit /b 1
)

echo.
echo ========================================
echo EJECUTANDO TESTS
echo ========================================
echo.

REM El ejecutable puede estar en net9.0-windows\net9.0 o directamente en net9.0-windows
if exist "bin\Release\net9.0-windows\net9.0\TestRustPack4.exe" (
    cd bin\Release\net9.0-windows\net9.0
    TestRustPack4.exe
    set TEST_RESULT=%ERRORLEVEL%
    cd ..\..\..\..
) else (
    if exist "bin\Release\net9.0-windows\TestRustPack4.exe" (
        cd bin\Release\net9.0-windows
        TestRustPack4.exe
        set TEST_RESULT=%ERRORLEVEL%
        cd ..\..\..
    ) else (
        echo ERROR: No se encontro TestRustPack4.exe
        set TEST_RESULT=1
    )
)

echo.
if %TEST_RESULT% EQU 0 (
    echo ========================================
    echo RESULTADO: RUST PACK 4 ES ESTABLE
    echo ========================================
    echo.
    echo Puedes reactivar Rust Pack 4 en MainFormOptimizations.cs:
    echo   private bool useRustPack4 = true;
    echo.
) else (
    echo ========================================
    echo RESULTADO: RUST PACK 4 NO ES ESTABLE
    echo ========================================
    echo.
    echo Mantener deshabilitado en MainFormOptimizations.cs:
    echo   private bool useRustPack4 = false;
    echo.
)

pause
