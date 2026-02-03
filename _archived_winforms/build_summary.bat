@echo off
dotnet build SlskDown.csproj --configuration Release > build_output.txt 2>&1
findstr /C:"Advertencia" /C:"Error" /C:"Tiempo transcurrido" build_output.txt
del build_output.txt
