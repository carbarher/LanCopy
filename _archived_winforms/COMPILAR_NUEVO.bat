@echo off
echo ========================================
echo Compilando con PROYECTO NUEVO LIMPIO
echo ========================================
echo.
echo 1. Backup del proyecto actual...
copy SlskDown.csproj SlskDown.csproj_backup
echo.
echo 2. Usando proyecto nuevo limpio...
copy SlskDown_NUEVO.csproj SlskDown.csproj
echo.
echo 3. Backup del MainForm actual...
copy MainForm.cs MainForm.cs_backup_completo
echo.
echo 4. Usando MainForm funcional...
copy MainForm_FUNCIONAL.cs MainForm.cs
echo.
echo 5. Compilando...
dotnet build SlskDown.csproj -c Release --verbosity normal
echo.
echo ========================================
echo Verificando resultado...
echo ========================================
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo.
    echo [EXITO] Ejecutable generado correctamente!
    echo.
    dir bin\Release\net8.0-windows\SlskDown.exe
    echo.
    echo Puedes ejecutarlo con: slsk.bat
) else (
    echo.
    echo [ERROR] No se genero el ejecutable
    echo.
)
echo ========================================
pause
