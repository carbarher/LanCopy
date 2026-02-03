@echo off
chcp 65001
echo Eliminando emojis de archivos .cs...
echo.

cd /d c:\p2p\SlskDown

for /r %%f in (*.cs) do (
    echo Procesando: %%~nxf
    powershell -NoProfile -Command "$content = Get-Content '%%f' -Raw -Encoding UTF8; if ($content -match '[🔍📁📂📄📊💾🔧⚙🚀🎯✅❌⚠🔴🟢⭐💡🔥⏰🌐🔗🔒🔑📝🎵💰📱💻⏸⏯⏹⏺▶➡⬅⬆⬇🔃🔄🔙🔚🔛🔜🔝⚡🌟💬💭🎉📦📧🗑📈📉👤]') { $content = $content -replace '[🔍📁📂📄📊💾🔧⚙🚀🎯✅❌⚠🔴🟢⭐💡🔥⏰🌐🔗🔒🔑📝🎵💰📱💻⏸⏯⏹⏺▶➡⬅⬆⬇🔃🔄🔙🔚🔛🔜🔝⚡🌟💬💭🎉📦📧🗑📈📉👤]',''; [System.IO.File]::WriteAllText('%%f', $content, [System.Text.UTF8Encoding]::new($false)); Write-Host 'Emojis eliminados' }"
)

echo.
echo Proceso completado
pause
