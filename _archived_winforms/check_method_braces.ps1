$lines = Get-Content "MainForm.cs"
$balance = 0
$methodStart = 20898
$methodEnd = 21087

Write-Host "Analizando metodo StartDownloadManager (lineas $methodStart - $methodEnd)"
Write-Host "="*60

for ($i = $methodStart - 1; $i -lt $methodEnd; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    $opens = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closes = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    
    $balance += $opens - $closes
    
    if ($opens -gt 0 -or $closes -gt 0) {
        $status = if ($balance -lt 0) { " ERROR" } else { "" }
        Write-Host "Linea $lineNum (balance: $balance)$status : $line"
    }
}

Write-Host "="*60
Write-Host "Balance final del metodo: $balance"
if ($balance -eq 0) {
    Write-Host "Metodo correcto" -ForegroundColor Green
} elseif ($balance -gt 0) {
    Write-Host "FALTAN $balance llave(s) de cierre" -ForegroundColor Red
} else {
    Write-Host "SOBRAN $([Math]::Abs($balance)) llave(s) de cierre" -ForegroundColor Red
}
