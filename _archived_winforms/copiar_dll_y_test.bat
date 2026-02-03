@echo off
echo ========================================
echo COPIAR DLL Y EJECUTAR TEST
echo ========================================
echo.

cd /d c:\p2p\SlskDown

echo [1/2] Copiando DLL compilada...
copy rust_core\target\release\slskdown_core.dll bin\Release\net9.0-windows\net9.0\slskdown_core.dll
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: No se pudo copiar la DLL
    pause
    exit /b 1
)

echo.
echo [2/2] Ejecutando test...
cd bin\Release\net9.0-windows\net9.0
TestRustPack4.exe

echo.
pause
