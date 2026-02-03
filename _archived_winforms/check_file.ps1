$lines = Get-Content 'MainForm.cs'
Write-Host "Total lines: $($lines.Count)"
Write-Host "Line 20303: $($lines[20302])"
Write-Host "Line 20304: $($lines[20303])"
Write-Host "Line 20305: $($lines[20304])"
