@echo off
echo ========================================
echo Compilando SlskDown...
echo ========================================
dotnet clean
dotnet build -c Release -v detailed > build_verbose_output.txt 2>&1
echo.
echo Resultado guardado en build_verbose_output.txt
type build_verbose_output.txt
