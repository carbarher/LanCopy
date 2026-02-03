$content = Get-Content "MainForm.cs" -Raw
$openBraces = ([regex]::Matches($content, '\{')).Count
$closeBraces = ([regex]::Matches($content, '\}')).Count
Write-Host "Llaves abiertas: $openBraces"
Write-Host "Llaves cerradas: $closeBraces"
Write-Host "Balance: $($openBraces - $closeBraces)"

if ($openBraces -ne $closeBraces) {
    Write-Host "ERROR: Llaves desbalanceadas!" -ForegroundColor Red
    
    # Encontrar dónde está el desbalance
    $lines = Get-Content "MainForm.cs"
    $balance = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $opens = ([regex]::Matches($line, '\{')).Count
        $closes = ([regex]::Matches($line, '\}')).Count
        $balance += $opens - $closes
        
        if ($balance -lt 0) {
            Write-Host "Línea $($i + 1): Balance negativo ($balance)" -ForegroundColor Yellow
            Write-Host "  $line"
            break
        }
    }
    
    Write-Host "`nBalance final en línea $($lines.Count): $balance"
} else {
    Write-Host "OK: Llaves balanceadas" -ForegroundColor Green
}
