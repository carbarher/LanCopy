@echo off
cd c:\p2p\SlskDown
dotnet build SlskDown.csproj -c Release --verbosity detailed > compile_detailed.log 2>&1
echo Exit code: %errorlevel% >> compile_detailed.log
type compile_detailed.log | findstr /C:"error" /C:"Build succeeded" /C:"Build FAILED"
