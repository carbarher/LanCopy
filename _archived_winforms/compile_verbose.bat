@echo off
cd /d c:\p2p\SlskDown
echo Restaurando backup...
copy /Y MainForm.cs.backup_full MainForm.cs >nul
echo.
echo Verificando llaves...
python find_brace.py
echo.
echo Limpiando...
dotnet clean >nul 2>&1
rmdir /s /q obj bin >nul 2>&1
dotnet build-server shutdown >nul 2>&1
echo.
echo Compilando con detalle...
dotnet build SlskDown.csproj -c Debug -v detailed > compile_log.txt 2>&1
echo.
echo Mostrando errores...
findstr /C:"error CS" compile_log.txt
echo.
echo Log completo guardado en: compile_log.txt
pause
