@echo off
echo ========================================
echo   VERIFICANDO CODIGO DE PESTANAS
echo ========================================
cd /d c:\p2p\SlskDown

echo.
echo Buscando definicion de pestanas...
findstr /N "TabPage.*Auto" MainForm.cs
echo.
echo Buscando CreateAutoTab...
findstr /N "CreateAutoTab" MainForm.cs
echo.
echo Buscando btnPurge...
findstr /N "btnPurge" MainForm.cs

echo.
echo ========================================
echo Presiona cualquier tecla para continuar...
pause >nul
