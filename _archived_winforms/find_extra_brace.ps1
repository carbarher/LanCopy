$lines = Get-Content "MainForm.cs"
$balance = 0
$methodStart = 0
$inMethod = $false

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Detectar inicio de ShowPerformanceStats
    if ($line -match "private void ShowPerformanceStats") {
        $methodStart = $lineNum
        $inMethod = $true
        $balance = 0
        Write-Host "Metodo ShowPerformanceStats encontrado en linea $lineNum"
    }
    
    if ($inMethod) {
        # Contar llaves
        $opens = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
        $closes = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        
        $balance += $opens - $closes
        
        if ($opens -gt 0 -or $closes -gt 0) {
            Write-Host "Linea $lineNum (balance: $balance): $line"
        }
        
        # Si el balance vuelve a 0, el metodo termino
        if ($balance -eq 0 -and $lineNum -gt $methodStart) {
            Write-Host "Metodo termina en linea $lineNum con balance $balance"
            break
        }
        
        if ($balance -lt 0) {
            Write-Host "ERROR: Balance negativo en linea $lineNum" -ForegroundColor Red
            break
        }
    }
}
