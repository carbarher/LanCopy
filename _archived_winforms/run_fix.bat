@echo off
echo Ejecutando script de correccion...
python comment_errors.py
if %ERRORLEVEL% EQU 0 (
    echo Script ejecutado exitosamente
) else (
    echo Error al ejecutar script: %ERRORLEVEL%
)
echo.
echo Verificando si se creo el backup...
if exist MainForm.cs.backup_before_fix (
    echo Backup creado correctamente
) else (
    echo ERROR: No se creo el backup
)
echo.
pause
