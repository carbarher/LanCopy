@echo off
echo Compilando...
dotnet build SlskDown.csproj -c Release /p:WarningLevel=0 > compile_errors.txt 2>&1
echo.
echo Errores encontrados:
findstr /C:"error CS" compile_errors.txt
echo.
echo Total de errores:
findstr /C:"Errores" compile_errors.txt
pause
