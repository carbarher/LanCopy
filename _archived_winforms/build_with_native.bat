@echo off
echo ========================================
echo Building SlskDown with Native Library
echo ========================================

echo.
echo [1/3] Building Rust native library...
cd slsk_native
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Rust build failed
    exit /b 1
)
cd ..

echo.
echo [2/3] Copying native DLL...
copy /Y slsk_native\target\release\slsk_native.dll bin\Release\net8.0-windows\slsk_native.dll
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Could not copy DLL
)

echo.
echo [3/3] Building C# project...
dotnet build .\SlskDown.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: C# build failed
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Native library: bin\Release\net8.0-windows\slsk_native.dll
echo Executable: bin\Release\net8.0-windows\SlskDown.exe
echo.
pause
