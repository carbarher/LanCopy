$content = Get-Content "MainForm.cs" -Raw
$opens = ([regex]::Matches($content, '\{')).Count
$closes = ([regex]::Matches($content, '\}')).Count
Write-Host "Aperturas: $opens"
Write-Host "Cierres: $closes"
Write-Host "Diferencia: $($opens - $closes)"
