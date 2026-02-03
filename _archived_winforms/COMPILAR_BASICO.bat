@echo off
cd /d c:\p2p\SlskDown
echo Compilando con MainForm basico...
dotnet build SlskDown.csproj -c Release -v detailed > build_basico.txt 2>&1
type build_basico.txt
pause
