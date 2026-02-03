@echo off
echo Instalando servicio de auto-commit cada hora...
echo.

REM Crear tarea programada que se ejecuta al iniciar el sistema
schtasks /create /tn "SlskDown_AutoCommit" /tr "c:\p2p\SlskDown\auto_commit_service.bat" /sc onstart /ru "%USERNAME%" /rl highest /f

if %errorlevel% equ 0 (
    echo.
    echo ✓ Tarea programada creada exitosamente
    echo.
    echo La tarea "SlskDown_AutoCommit" se ejecutara:
    echo - Al iniciar Windows
    echo - Hara commits automaticos cada hora
    echo - Se ejecutara en segundo plano
    echo.
    echo Para iniciar ahora sin reiniciar:
    echo   schtasks /run /tn "SlskDown_AutoCommit"
    echo.
    echo Para detener:
    echo   schtasks /end /tn "SlskDown_AutoCommit"
    echo.
    echo Para eliminar:
    echo   schtasks /delete /tn "SlskDown_AutoCommit" /f
    echo.
) else (
    echo.
    echo × Error al crear la tarea programada
    echo Ejecuta este script como Administrador
    echo.
)

pause
