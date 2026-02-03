@echo off
dotnet build SlskDown.csproj -c Release 2>&1 | findstr /C:"error" /C:"Errores" /C:"correctamente"
