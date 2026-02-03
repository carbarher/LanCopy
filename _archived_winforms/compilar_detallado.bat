@echo off
dotnet build -c Release -v detailed > build_log.txt 2>&1
type build_log.txt
echo.
echo Buscando ejecutable...
dir /S /B *.exe 2>nul
pause
