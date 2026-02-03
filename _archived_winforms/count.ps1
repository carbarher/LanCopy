$lines = Get-Content "MainForm.cs"
$opens = 0
$closes = 0

foreach ($line in $lines) {
    $opens += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closes += ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
}

Write-Output "Aperturas: $opens"
Write-Output "Cierres: $closes"
Write-Output "Diferencia: $($opens - $closes)"
