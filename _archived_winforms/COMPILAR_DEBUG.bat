@echo off
cd /d c:\p2p\SlskDown
echo Limpiando...
dotnet clean > compile_output.txt 2>&1
echo.
echo Compilando...
dotnet build -c Release -v detailed >> compile_output.txt 2>&1
echo.
echo Resultado de compilacion guardado en compile_output.txt
type compile_output.txt
echo.
echo.
echo Buscando ejecutable...
dir /s /b bin\*.exe 2>nul
echo.
pause
