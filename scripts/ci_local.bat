@echo off
setlocal enableextensions

echo ===============================================
echo CI LOCAL (build + tests) - SlskDown
echo ===============================================

rem Build without apphost to avoid locking SlskDown.exe during iterative dev
dotnet build c:\p2p\SlskDown\SlskDown.csproj -c Release /p:UseAppHost=false
if errorlevel 1 (
  echo.
  echo ERROR: build fallo.
  echo Nota: si ves MSB3027/MSB3021, cierra SlskDown/TestCreds o libera el lock del exe.
  exit /b 1
)

echo.
echo ===============================================
echo Running tests
echo ===============================================

dotnet test c:\p2p\SlskDown.Tests\SlskDown.Tests.csproj -c Release
if errorlevel 1 (
  echo.
  echo ERROR: tests fallaron.
  exit /b 1
)

echo.
echo OK: build + tests completados
exit /b 0
