@echo off
dotnet msbuild SlskDown.csproj -t:Rebuild -p:Configuration=Release -v:detailed > msbuild_detailed.log 2>&1
echo Compilacion terminada. Ver msbuild_detailed.log
pause
