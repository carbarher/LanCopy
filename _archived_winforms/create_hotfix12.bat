@echo off
echo Creando directorio publish_hotfix12...
if not exist bin\publish_hotfix12 mkdir bin\publish_hotfix12
echo Copiando archivos...
xcopy /E /I /Y /Q bin\Release\net8.0-windows\* bin\publish_hotfix12\
echo.
echo Verificando resultado...
if exist bin\publish_hotfix12\SlskDown.exe (
    echo [OK] Build creado exitosamente en bin\publish_hotfix12
    dir bin\publish_hotfix12\SlskDown.exe
) else (
    echo [ERROR] No se pudo crear el build
)
