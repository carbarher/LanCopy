@echo off
del /Q build_output_new.txt 2>nul
echo Compilando... > build_output_new.txt
dotnet clean >> build_output_new.txt 2>&1
echo. >> build_output_new.txt
dotnet build -c Release >> build_output_new.txt 2>&1
echo. >> build_output_new.txt
echo ========================================== >> build_output_new.txt
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo [OK] Ejecutable generado >> build_output_new.txt
) else (
    echo [ERROR] No se genero ejecutable >> build_output_new.txt
)
