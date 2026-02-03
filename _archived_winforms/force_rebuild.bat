@echo off
cd /d c:\p2p\SlskDown

echo === CERRANDO SLSKDOWN ===
taskkill /F /IM SlskDown.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo === LIMPIANDO TODO ===
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo.
echo === COMPILANDO ===
dotnet build SlskDown.csproj -c Release --no-incremental

echo.
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo === EXITO ===
    dir "bin\Release\net8.0-windows\SlskDown.exe"
    echo.
    echo Fecha y hora del ejecutable:
    forfiles /p "bin\Release\net8.0-windows" /m SlskDown.exe /c "cmd /c echo @fdate @ftime"
) else (
    echo === ERROR ===
)

pause
