@echo off
REM Auto-commit service - Se ejecuta continuamente
cd /d "c:\p2p\SlskDown"

:loop
REM Hacer commit de todos los cambios
git add -A
git commit -m "Auto-save: %date:~-4,4%%date:~-10,2%%date:~-7,2%_%time:~0,2%%time:~3,2%%time:~6,2%" 2>nul

REM Esperar 1 hora (3600 segundos)
timeout /t 3600 /nobreak >nul

goto loop
