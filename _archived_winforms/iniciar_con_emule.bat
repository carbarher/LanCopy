@echo off
echo ========================================
echo Inicio SlskDown con eMule
echo ========================================
echo.

echo [1/2] Verificando eMule...
tasklist | findstr /I "emule.exe" >nul
if %errorlevel% equ 0 (
    echo ✅ eMule ya esta corriendo
) else (
    echo ⚠️ eMule NO esta corriendo
    echo    Intentando iniciar eMule...
    
    if exist "C:\Program Files\eMule\emule.exe" (
        start "" "C:\Program Files\eMule\emule.exe"
        echo ✅ eMule iniciado desde C:\Program Files\eMule\
        timeout /t 3 >nul
    ) else if exist "C:\Program Files (x86)\eMule\emule.exe" (
        start "" "C:\Program Files (x86)\eMule\emule.exe"
        echo ✅ eMule iniciado desde C:\Program Files (x86)\eMule\
        timeout /t 3 >nul
    ) else (
        echo ❌ eMule no encontrado en ubicaciones comunes
        echo    SlskDown funcionara solo con Soulseek
    )
)
echo.

echo [2/2] Iniciando SlskDown...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ Iniciando SlskDown...
    cd bin\Release\net8.0-windows
    start SlskDown.exe
    echo.
    echo ========================================
    echo ✅ SlskDown iniciado
    echo ========================================
    echo.
    tasklist | findstr /I "emule.exe" >nul
    if %errorlevel% equ 0 (
        echo 🌐 Modo: Multi-Red (Soulseek + eMule)
    ) else (
        echo 📡 Modo: Solo Soulseek
    )
) else (
    echo ❌ SlskDown.exe no encontrado
    echo    Compila el proyecto primero
)
echo.
pause
