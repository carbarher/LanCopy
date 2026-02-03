@echo off
echo Copiando ejecutable...
if exist "obj\Release\net8.0-windows\apphost.exe" (
    echo Archivo encontrado
    copy /Y "obj\Release\net8.0-windows\apphost.exe" "SlskDown.exe"
    if exist "SlskDown.exe" (
        echo EXE copiado exitosamente
        dir SlskDown.exe
    ) else (
        echo ERROR: No se pudo copiar el EXE
    )
) else (
    echo ERROR: No existe obj\Release\net8.0-windows\apphost.exe
)
pause
