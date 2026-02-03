@echo off
echo Obteniendo lista de archivos compilados...
dotnet msbuild SlskDown.csproj /t:CoreCompile /p:Configuration=Debug /v:detailed /flp:logfile=detailed_build.log;verbosity=detailed
echo.
echo Extrayendo archivos .cs...
findstr /C:".cs " detailed_build.log | findstr /V /C:"error" | findstr /V /C:"warning" > compiled_files.txt
echo.
echo Archivos guardados en: compiled_files.txt
type compiled_files.txt
pause
