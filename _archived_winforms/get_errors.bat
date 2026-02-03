@echo off
echo Compilando...
dotnet build SlskDown.csproj --no-incremental > compile_result.txt 2>&1
echo.
echo Errores encontrados:
findstr /C:"error CS" compile_result.txt
echo.
echo Resultado guardado en compile_result.txt
pause
