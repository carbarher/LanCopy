@echo off
echo === VERIFICANDO DOTNET ===
dotnet --version > test_output.txt 2>&1
echo. >> test_output.txt
echo === COMPILANDO === >> test_output.txt
dotnet build -c Release >> test_output.txt 2>&1
echo. >> test_output.txt
echo === RESULTADO === >> test_output.txt
dir bin\Release\net8.0-windows\*.exe >> test_output.txt 2>&1
echo LISTO - Ver test_output.txt
type test_output.txt
