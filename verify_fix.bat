@echo off
cd /d c:\p2p\SlskDown
echo Verificando cambios en MainForm.cs...
echo.
findstr /n "private ListView lvFiles;" MainForm.cs
echo.
findstr /n "lvFiles = new ListView" MainForm.cs
echo.
echo Compilando...
dotnet build SlskDown.csproj -c Release --verbosity minimal
echo.
if %ERRORLEVEL% EQU 0 (
    echo ✅ COMPILACION EXITOSA
) else (
    echo ❌ ERROR DE COMPILACION
)
pause
