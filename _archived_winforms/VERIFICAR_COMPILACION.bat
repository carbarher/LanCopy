@echo off
cd /d c:\p2p\SlskDown

echo ========================================
echo VERIFICACION DE COMPILACION
echo ========================================
echo.

echo Verificando ejecutable...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [OK] Ejecutable existe
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    
    echo Verificando DLLs necesarias...
    if exist "bin\Release\net8.0-windows\Soulseek.dll" (
        echo [OK] Soulseek.dll existe
    ) else (
        echo [ERROR] Soulseek.dll NO existe
    )
    
    if exist "bin\Release\net8.0-windows\Newtonsoft.Json.dll" (
        echo [OK] Newtonsoft.Json.dll existe
    ) else (
        echo [ERROR] Newtonsoft.Json.dll NO existe
    )
    
    echo.
    echo Intentando ejecutar...
    echo Si no se abre, presiona Ctrl+C y ejecuta EJECUTAR_CON_LOGS.bat
    echo.
    pause
    
    start "" "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo [ERROR] Ejecutable NO existe
    echo.
    echo Ejecuta primero: COMPILAR_FORZADO.bat
)

echo.
pause
