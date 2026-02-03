@echo off
echo ========================================
echo BACKUP Y COMMIT - SlskDown
echo ========================================
echo.

REM Crear carpeta de backup con timestamp
set TIMESTAMP=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set BACKUP_DIR=backups\backup_%TIMESTAMP%

echo Creando backup en: %BACKUP_DIR%
mkdir "%BACKUP_DIR%" 2>nul

echo Copiando archivos del proyecto SlskDown...
xcopy SlskDown "%BACKUP_DIR%\SlskDown" /E /I /H /Y /Q

echo.
echo ========================================
echo COMMIT A GIT
echo ========================================
echo.

REM Verificar si git está disponible
git --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git no está instalado o no está en el PATH
    pause
    exit /b 1
)

REM Mostrar estado actual
echo Estado actual:
git status -s

echo.
echo Agregando archivos...
git add .

echo.
echo Haciendo commit...
git commit -m "Backup pre-limpieza - SlskDown estado actual %TIMESTAMP%"

echo.
echo Último commit:
git log -1 --oneline

echo.
echo ========================================
echo COMPLETADO
echo ========================================
echo Backup creado en: %BACKUP_DIR%
echo Commit realizado exitosamente
echo.
pause
