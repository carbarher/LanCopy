@echo off
cd c:\p2p\SlskDown
echo Compilando...
dotnet build SlskDown.csproj -c Release --verbosity normal --nologo > c:\p2p\compile_output.txt 2>&1
echo Exit code: %errorlevel%
type c:\p2p\compile_output.txt
