@echo off
echo Compilando proyecto...
echo.

dotnet build SlskDown.csproj -c Release > compile_output.txt 2>&1

echo Compilacion terminada. Buscando errores...
findstr /C:"error CS" compile_output.txt > errors_only.txt

echo.
echo === RESUMEN ===
findstr /C:"Errores" compile_output.txt
echo.
echo Los errores se guardaron en: errors_only.txt
echo El output completo esta en: compile_output.txt
echo.
pause
