@echo off
dotnet build 2>&1 | findstr /C:"error CS" > errors_temp.txt
type errors_temp.txt
