@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo Eliminando emojis de archivos .cs en SlskDown...
echo.

set "project_path=c:\p2p\SlskDown"
set "files_processed=0"

for /r "%project_path%" %%f in (*.cs) do (
    echo Procesando: %%~nxf
    
    REM Crear archivo temporal
    set "temp_file=%%f.tmp"
    
    REM Procesar archivo línea por línea eliminando emojis comunes
    (for /f "usebackq delims=" %%l in ("%%f") do (
        set "line=%%l"
        REM Eliminar emojis más comunes (los que causan problemas en batch se omiten)
        set "line=!line:🔍=!"
        set "line=!line:📁=!"
        set "line=!line:📂=!"
        set "line=!line:📄=!"
        set "line=!line:📊=!"
        set "line=!line:💾=!"
        set "line=!line:🔧=!"
        set "line=!line:⚙=!"
        set "line=!line:🚀=!"
        set "line=!line:🎯=!"
        set "line=!line:✅=!"
        set "line=!line:❌=!"
        set "line=!line:⚠=!"
        set "line=!line:🔴=!"
        set "line=!line:🟢=!"
        set "line=!line:⭐=!"
        set "line=!line:💡=!"
        set "line=!line:🔥=!"
        set "line=!line:⏰=!"
        set "line=!line:🌐=!"
        set "line=!line:🔗=!"
        set "line=!line:🔒=!"
        set "line=!line:🔑=!"
        set "line=!line:📝=!"
        set "line=!line:🎵=!"
        set "line=!line:💰=!"
        set "line=!line:📱=!"
        set "line=!line:💻=!"
        set "line=!line:⏸=!"
        set "line=!line:⏯=!"
        set "line=!line:⏹=!"
        set "line=!line:⏺=!"
        set "line=!line:▶=!"
        set "line=!line:➡=!"
        set "line=!line:⬅=!"
        set "line=!line:⬆=!"
        set "line=!line:⬇=!"
        set "line=!line:🔃=!"
        set "line=!line:🔄=!"
        set "line=!line:🔙=!"
        set "line=!line:🔚=!"
        set "line=!line:🔛=!"
        set "line=!line:🔜=!"
        set "line=!line:🔝=!"
        set "line=!line:⚡=!"
        set "line=!line:🌟=!"
        set "line=!line:💬=!"
        set "line=!line:💭=!"
        set "line=!line:🎉=!"
        set "line=!line:📦=!"
        set "line=!line:📧=!"
        set "line=!line:🗑=!"
        set "line=!line:📈=!"
        set "line=!line:📉=!"
        set "line=!line:👤=!"
        set "line=!line:🔎=!"
        set "line=!line:🔐=!"
        set "line=!line:🔓=!"
        set "line=!line:🔨=!"
        set "line=!line:⏱=!"
        set "line=!line:🔋=!"
        set "line=!line:🔌=!"
        set "line=!line:💸=!"
        set "line=!line:💵=!"
        set "line=!line:💳=!"
        set "line=!line:💎=!"
        echo !line!
    )) > "!temp_file!"
    
    REM Reemplazar archivo original
    move /y "!temp_file!" "%%f" >nul
    set /a files_processed+=1
)

echo.
echo ==========================================
echo Archivos procesados: %files_processed%
echo ==========================================
pause
