@echo off
echo Creando backup...
copy MainForm.cs MainForm.cs.backup_manual >nul
echo Eliminando lineas duplicadas con PowerShell...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$lines = Get-Content 'MainForm.cs'; Write-Host \"Lineas originales: $($lines.Length)\"; $newLines = $lines[0..1805] + $lines[27894..($lines.Length-1)]; $newLines | Set-Content 'MainForm.cs.temp'; Move-Item -Force 'MainForm.cs.temp' 'MainForm.cs'; $final = Get-Content 'MainForm.cs'; Write-Host \"Lineas finales: $($final.Length)\"; Write-Host \"Eliminadas: $(($lines.Length) - ($final.Length)) lineas\""
echo.
echo Listo. Ejecuta 'lanza' para compilar.
pause
