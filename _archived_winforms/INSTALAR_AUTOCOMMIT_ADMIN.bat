@echo off
echo ========================================
echo Instalando Auto-Commit Permanente
echo ========================================
echo.

cd /d "c:\p2p\SlskDown"

REM Crear tarea programada
schtasks /create /tn "SlskDown_AutoCommit" /tr "c:\p2p\SlskDown\auto_commit_service.bat" /sc onstart /ru "%USERNAME%" /rl highest /f

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo   INSTALACION EXITOSA
    echo ========================================
    echo.
    echo La tarea "SlskDown_AutoCommit" se ejecutara:
    echo   - Al iniciar Windows
    echo   - Hara commits automaticos cada hora
    echo   - Se ejecutara en segundo plano
    echo.
    echo Para iniciar ahora sin reiniciar:
    echo   schtasks /run /tn "SlskDown_AutoCommit"
    echo.
    echo Para verificar estado:
    echo   schtasks /query /tn "SlskDown_AutoCommit"
    echo.
    echo Para detener:
    echo   schtasks /end /tn "SlskDown_AutoCommit"
    echo.
    echo Para eliminar:
    echo   schtasks /delete /tn "SlskDown_AutoCommit" /f
    echo.
    echo ========================================
    echo Iniciando el servicio ahora...
    echo ========================================
    schtasks /run /tn "SlskDown_AutoCommit"
    echo.
    echo Auto-commit activado y funcionando!
) else (
    echo.
    echo ========================================
    echo   ERROR
    echo ========================================
    echo.
    echo No se pudo crear la tarea programada.
    echo Este script debe ejecutarse como Administrador.
    echo.
    echo Haz clic derecho en este archivo y selecciona:
    echo "Ejecutar como administrador"
)

echo.
pause
