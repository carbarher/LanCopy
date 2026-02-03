@echo off
REM Script de limpieza para SlskDown
REM Elimina archivos de ejemplo que causan errores de compilacion

cd /d "%~dp0"

echo Limpiando archivos de ejemplo...

if exist "MigrateToSecure.cs" (
    del /F /Q "MigrateToSecure.cs"
    echo - MigrateToSecure.cs eliminado
)

if exist "MainFormIntegration.cs" (
    del /F /Q "MainFormIntegration.cs"
    echo - MainFormIntegration.cs eliminado
)

echo.
echo Limpieza completada.
echo.
pause
