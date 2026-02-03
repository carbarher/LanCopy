@echo off
echo Arreglando errores restantes...

REM Comentar líneas con .ThenBy que causan error
powershell -Command "(Get-Content 'MainForm.cs') -replace '(\s+)\.ThenBy\(t => t\.StartTime\)', '$1// .ThenBy(t => t.StartTime)' | Set-Content 'MainForm.cs'"
powershell -Command "(Get-Content 'MainForm.cs') -replace '(\s+)\.ThenBy\(t => t\.Status\)', '$1// .ThenBy(t => t.Status)' | Set-Content 'MainForm.cs'"

REM Arreglar TimeSpan?.TotalSeconds
powershell -Command "$content = Get-Content 'MainForm.cs' -Raw; $content = $content -replace '(\w+)\?\.TotalSeconds', '($1.HasValue ? $1.Value.TotalSeconds : 0)'; Set-Content 'MainForm.cs' -Value $content -NoNewline"

echo Listo!
dotnet build SlskDown.csproj -c Release
pause
