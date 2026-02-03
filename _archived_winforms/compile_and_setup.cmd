@echo off
echo ========================================
echo Compilando SlskDown con integracion Rust
echo ========================================
echo.

echo [1/3] Compilando proyecto C#...
dotnet build SlskDown.csproj -c Debug -v minimal
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Fallo la compilacion de C#
    pause
    exit /b 1
)

echo.
echo [2/3] Verificando DLL de Rust...
if not exist "RustCore\target\release\slskdown_core.dll" (
    echo ADVERTENCIA: DLL de Rust no encontrada
    echo Ejecuta primero: RustCore\clean_and_build.cmd
    echo.
    echo La app funcionara con fallback C# (mas lento^)
    pause
    exit /b 0
)

echo.
echo [3/3] Copiando DLL de Rust a directorios de salida...
if not exist "bin\Debug" mkdir "bin\Debug"
if not exist "bin\Release" mkdir "bin\Release"
copy /Y "RustCore\target\release\slskdown_core.dll" "bin\Debug\" >nul
copy /Y "RustCore\target\release\slskdown_core.dll" "bin\Release\" >nul

echo.
echo ========================================
echo COMPILACION EXITOSA
echo ========================================
echo.
echo Archivos generados:
dir /b bin\Debug\SlskDown.exe 2>nul
dir /b bin\Debug\slskdown_core.dll 2>nul
echo.
echo Para ejecutar: bin\Debug\SlskDown.exe
echo.
pause
