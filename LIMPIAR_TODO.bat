@echo off
echo Limpiando proyecto...

REM Eliminar archivos temporales en raíz
del /F /Q *.jpg *.log *.txt *.exe *.db *.csv *.rtf *.djvu *.py *.ps1 *.md *.html *.toml *.json *.cs 2>nul

REM Eliminar scripts BAT obsoletos (mantener solo este)
for %%f in (*.bat) do (
    if not "%%f"=="LIMPIAR_TODO.bat" del /F /Q "%%f" 2>nul
)

REM Eliminar carpetas de proyectos obsoletos
rd /S /Q AppFinalSinVerde 2>nul
rd /S /Q SinVerdeApp 2>nul
rd /S /Q SlskClean 2>nul
rd /S /Q SlskDown2025 2>nul
rd /S /Q SlskDownAutoresNuevo 2>nul
rd /S /Q SlskDownFixed 2>nul
rd /S /Q SlskDownFramework 2>nul
rd /S /Q SlskDownFull 2>nul
rd /S /Q SlskDownMinimal 2>nul
rd /S /Q SlskDownSimple 2>nul
rd /S /Q SlskDownloadEngine 2>nul
rd /S /Q SlskFresh 2>nul
rd /S /Q SoulseekDownloader 2>nul
rd /S /Q SoulseekNuevo 2>nul
rd /S /Q SlskDown.Tests 2>nul

REM Eliminar carpetas de Rust y binarios
rd /S /Q slskdown-core 2>nul
rd /S /Q slskdown_native 2>nul
rd /S /Q slskdown_rust 2>nul

REM Eliminar carpetas temporales
rd /S /Q backups 2>nul
rd /S /Q downloads 2>nul
rd /S /Q exports 2>nul
rd /S /Q logs 2>nul
rd /S /Q incomplete 2>nul
rd /S /Q __pycache__ 2>nul
rd /S /Q .cache 2>nul

REM Limpiar binarios de SlskDown
rd /S /Q SlskDown\bin 2>nul
rd /S /Q SlskDown\obj 2>nul
del /F /Q SlskDown\*.user 2>nul
del /F /Q SlskDown\*.suo 2>nul

echo.
echo Limpieza completada!
echo Solo quedan: .git, .gitignore, .vscode, SlskDown (proyecto principal)
pause
