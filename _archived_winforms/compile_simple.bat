@echo off
cd /d c:\p2p\SlskDown
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
dotnet build SlskDown.csproj -c Release
pause
