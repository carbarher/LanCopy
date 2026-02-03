@echo off
echo ========================================
echo Creando Acceso Directo en Escritorio
echo ========================================
echo.

REM Crear acceso directo usando PowerShell
powershell -Command "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\SlskDown Multi-Red.lnk'); $Shortcut.TargetPath = '%CD%\SlskDown\SlskDown_MultiRed.bat'; $Shortcut.WorkingDirectory = '%CD%\SlskDown'; $Shortcut.IconLocation = '%CD%\SlskDown\bin\Release\net8.0-windows\SlskDown.exe,0'; $Shortcut.Description = 'Iniciar SlskDown con soporte Multi-Red automatico'; $Shortcut.Save()"

if exist "%USERPROFILE%\Desktop\SlskDown Multi-Red.lnk" (
    echo ✅ Acceso directo creado en el escritorio
    echo    Nombre: "SlskDown Multi-Red"
    echo.
    echo ✅ Ahora puedes iniciar SlskDown desde el escritorio
    echo    El script iniciara eMule automaticamente si es necesario
) else (
    echo ❌ No se pudo crear el acceso directo
    echo    Puedes ejecutar manualmente: SlskDown_MultiRed.bat
)
echo.
echo ========================================
pause
