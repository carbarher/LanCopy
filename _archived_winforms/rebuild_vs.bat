@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
cd /d c:\p2p\SlskDown
msbuild SlskDown.csproj /t:Rebuild /p:Configuration=Release /v:minimal
echo.
echo Compilacion completada
pause
