@echo off
echo Compilando...
dotnet build SlskDown.csproj -c Release 2>&1 | findstr /C:"error CS" /C:"Errores" /C:"correctamente" > compile_result_now.txt
type compile_result_now.txt
echo.
echo Resultado guardado en compile_result_now.txt
pause
