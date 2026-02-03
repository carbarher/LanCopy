@echo off
dotnet build -c Release > build_result.txt 2>&1
type build_result.txt

type build_output.txt
pause
