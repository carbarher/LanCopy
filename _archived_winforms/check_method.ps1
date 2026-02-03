$lines = Get-Content "MainForm.cs"
$startLine = 25920 - 1  # Array is 0-indexed
$endLine = 26167 - 1

$balance = 0
$opens = 0
$closes = 0

for ($i = $startLine; $i -le $endLine; $i++) {
    $line = $lines[$i]
    $o = ([regex]::Matches($line, '\{')).Count
    $c = ([regex]::Matches($line, '\}')).Count
    $opens += $o
    $closes += $c
    $balance += $o - $c
    
    if ($o -gt 0 -or $c -gt 0) {
        Write-Host "Línea $($i + 1): +$o -$c (balance: $balance) | $($line.Trim())"
    }
}

Write-Host "`n=== RESUMEN ==="
Write-Host "Llaves abiertas: $opens"
Write-Host "Llaves cerradas: $closes"
Write-Host "Balance: $balance"

if ($balance -ne 0) {
    Write-Host "ERROR: Faltan $([Math]::Abs($balance)) llaves de $(if ($balance -gt 0) {'cierre'} else {'apertura'})" -ForegroundColor Red
} else {
    Write-Host "OK: Método balanceado" -ForegroundColor Green
}
