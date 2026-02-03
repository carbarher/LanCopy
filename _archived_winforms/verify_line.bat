@echo off
powershell -NoProfile -Command "$line = (Get-Content MainForm.cs)[19338]; if ($line -match '^\s*}\s*$') { Write-Host 'ERROR: Linea 19339 TIENE cierre de clase' } else { Write-Host 'OK: Linea 19339 NO tiene cierre de clase' }"
pause
