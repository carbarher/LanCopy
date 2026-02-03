@echo off
cd /d c:\p2p\SlskDown
dotnet build SlskDown.csproj -c Release > errores.txt 2>&1
type errores.txt
pause
