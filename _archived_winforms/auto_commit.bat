@echo off
echo ========================================
echo AUTO-COMMIT: Guardando cambios en Git
echo ========================================

cd /d C:\p2p\SlskDown

REM Agregar todos los cambios
git add -A

REM Crear commit con timestamp
set timestamp=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set timestamp=%timestamp: =0%
git commit -m "Auto-save: %timestamp%"

echo.
echo ========================================
echo Cambios guardados en Git local
echo ========================================
echo.
echo Para subir a GitHub: git push
echo ========================================
