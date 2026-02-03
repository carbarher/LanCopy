@echo off
cls
echo Arreglando errores...
python fix_errors.py
echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release
pause
