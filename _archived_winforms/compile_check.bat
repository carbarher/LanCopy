@echo off
cd /d c:\p2p\SlskDown
echo Compilando...
dotnet clean > nul 2>&1
dotnet build -c Release > compile_log.txt 2>&1
echo.
findstr /C:"error CS" compile_log.txt > errors_only.txt 2>nul
if %errorlevel% equ 0 (
    echo ERRORES ENCONTRADOS:
    type errors_only.txt
) else (
    echo SIN ERRORES - Verificando ejecutable...
    if exist bin\Release\net8.0-windows\SlskDown.exe (
        echo COMPILACION EXITOSA - Ejecutable generado
    ) else (
        echo ADVERTENCIA - No se encontro ejecutable
    )
)
pause
