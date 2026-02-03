@echo off
cd /d c:\p2p\SlskDown
echo Compilando backup del 3 de noviembre...
dotnet build SlskDown.csproj -c Release > compilar_backup.txt 2>&1
type compilar_backup.txt
echo.
if exist bin\Release\net8.0-windows\SlskDown.exe (
    echo ========================================
    echo EXITO - Ejecutable creado
    echo ========================================
    dir bin\Release\net8.0-windows\SlskDown.exe
) else (
    echo ========================================
    echo ERROR - No se creo el ejecutable
    echo ========================================
)
pause
