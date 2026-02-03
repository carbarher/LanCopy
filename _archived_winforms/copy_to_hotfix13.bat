@echo off
echo Copiando archivos a publish_hotfix13...
xcopy /E /I /Y /Q bin\Release\net8.0-windows bin\publish_hotfix13
if exist bin\publish_hotfix13\SlskDown.exe (
    echo [OK] Build creado en bin\publish_hotfix13
) else (
    echo [ERROR] No se pudo crear el build
)
