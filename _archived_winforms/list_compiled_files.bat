@echo off
echo Listando archivos .cs que MSBuild compilara...
echo.
dotnet msbuild SlskDown.csproj /t:CoreCompile /p:Configuration=Debug /v:detailed > msbuild_files.txt 2>&1
findstr /C:"MainForm" msbuild_files.txt | findstr /V /C:"Designer"
echo.
echo Log completo en: msbuild_files.txt
pause
