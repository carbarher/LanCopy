@echo off
cd /d c:\p2p\SlskDownAvalonia
echo Limpiando...
rmdir /S /Q bin obj 2>nul
echo.
echo Compilando...
dotnet build SlskDownAvalonia.csproj -c Release 2>&1 | findstr /C:"error CS" /C:"warning CS" /C:"Build succeeded" /C:"Build FAILED"
echo.
if exist "bin\Release\net9.0\SlskDownAvalonia.exe" (
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
) else (
    echo ========================================
    echo COMPILACION FALLIDA - Ver errores arriba
    echo ========================================
)
pause
