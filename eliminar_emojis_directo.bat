@echo off
chcp 65001 >nul
echo ============================================
echo ELIMINADOR DE EMOJIS - SlskDown
echo ============================================
echo.

cd /d "c:\p2p\SlskDown"

echo Procesando archivos .cs...
echo.

for /r %%f in (*.cs) do (
    echo Procesando: %%~nxf
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$content = [System.IO.File]::ReadAllText('%%f', [System.Text.Encoding]::UTF8); $original = $content.Length; $content = $content -replace '[\u2600-\u27BF]', ''; $content = $content -replace '[\uD83C-\uDBFF][\uDC00-\uDFFF]', ''; $content = $content -replace '\uFE0F', ''; if ($content.Length -lt $original) { [System.IO.File]::WriteAllText('%%f', $content, [System.Text.Encoding]::UTF8); Write-Host '  Eliminados: ' ($original - $content.Length) ' caracteres' -ForegroundColor Green } else { Write-Host '  Sin cambios' -ForegroundColor Gray }"
)

echo.
echo ============================================
echo PROCESO COMPLETADO
echo ============================================
pause
