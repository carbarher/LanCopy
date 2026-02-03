@echo off
echo Verificando restauracion...
echo.
echo === Lineas del archivo actual ===
find /c /v "" MainForm.cs
echo.
echo === Lineas del backup_full ===
find /c /v "" MainForm.cs.backup_full
echo.
echo === Buscando using SlskDown.Database en actual ===
findstr /C:"using SlskDown.Database" MainForm.cs
echo ErrorLevel: %ERRORLEVEL%
echo.
echo === Buscando using SlskDown.Database en backup ===
findstr /C:"using SlskDown.Database" MainForm.cs.backup_full
echo ErrorLevel: %ERRORLEVEL%
