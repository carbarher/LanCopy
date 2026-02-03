@echo off
cd /d C:\p2p\SlskDown
echo Compilando...
dotnet build SlskDown.csproj -c Release > errores_compilacion.log 2>&1
echo Compilacion terminada
echo Ver errores_compilacion.log
type errores_compilacion.log | findstr /i "error"
pause
