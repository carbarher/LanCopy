@echo off
echo ========================================
echo Inicio Rapido SlskDown Multi-Red
echo ========================================
echo.

echo [1/3] Verificando aMule daemon...
netstat -ano | findstr :4712 >nul
if %errorlevel% equ 0 (
    echo ✅ aMule daemon esta corriendo en puerto 4712
) else (
    echo ⚠️ aMule daemon NO esta corriendo
    echo    Iniciando aMule daemon...
    if exist "C:\Program Files\aMule\amuled.exe" (
        start "" "C:\Program Files\aMule\amuled.exe"
        timeout /t 3 >nul
        echo ✅ aMule daemon iniciado
    ) else (
        echo ❌ aMule no encontrado en C:\Program Files\aMule\
        echo    Instala aMule de: https://www.amule.org/
    )
)
echo.

echo [2/3] Verificando binario SlskDown...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ SlskDown.exe encontrado
) else (
    echo ❌ SlskDown.exe no encontrado
    echo    Compila el proyecto primero
    goto :error
)
echo.

echo [3/3] Iniciando SlskDown...
echo.
echo ========================================
echo INSTRUCCIONES:
echo ========================================
echo 1. Ve a Configuracion
echo 2. Activa "Habilitar eMule/ed2k"
echo 3. Reinicia SlskDown
echo 4. Realiza una busqueda
echo 5. Verifica resultados de ambas redes
echo ========================================
echo.
echo Iniciando en 3 segundos...
timeout /t 3 >nul

cd bin\Release\net8.0-windows
start SlskDown.exe

echo.
echo ✅ SlskDown iniciado con soporte multi-red
echo.
goto :end

:error
echo.
echo ❌ Error en inicio
pause
exit /b 1

:end
echo Presiona cualquier tecla para cerrar...
pause >nul
