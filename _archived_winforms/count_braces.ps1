$lines = Get-Content "MainForm.cs"
$opens = 0
$closes = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Contar llaves (método simple, puede tener falsos positivos en strings)
    $opens += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closes += ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
}

Write-Host " ANÁLISIS DE LLAVES:"
Write-Host "Llaves de apertura '{': $opens"
Write-Host "Llaves de cierre '}': $closes"
Write-Host "Diferencia: $($opens - $closes)"
Write-Host ""

if ($opens -eq $closes) {
    Write-Host " Las llaves están balanceadas (conteo simple)" -ForegroundColor Green
} elseif ($opens -gt $closes) {
    Write-Host " FALTAN $($opens - $closes) llave(s) de cierre" -ForegroundColor Red
} else {
    Write-Host " SOBRAN $($closes - $opens) llave(s) de cierre" -ForegroundColor Red
}
