$lines = Get-Content "MainForm.cs"
$balance = 0
$methodStack = @()

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Detectar inicio de método
    if ($line -match '^\s*(private|public|protected|internal)\s+(void|async|Task|int|string|bool)') {
        $methodStack += @{Line = $lineNum; Method = $line.Trim().Substring(0, [Math]::Min(60, $line.Trim().Length))}
    }
    
    $opens = ([regex]::Matches($line, '\{')).Count
    $closes = ([regex]::Matches($line, '\}')).Count
    $balance += $opens - $closes
    
    # Si el balance llega a 1 y estamos después de la línea 26000, mostrar info
    if ($balance -eq 1 -and $lineNum -gt 26000) {
        Write-Host "`nLínea $lineNum : Balance = $balance (debería ser 2 para clase+namespace)"
        Write-Host "Métodos en stack:"
        foreach ($m in $methodStack | Select-Object -Last 5) {
            Write-Host "  Línea $($m.Line): $($m.Method)"
        }
        break
    }
}

Write-Host "`nBalance final: $balance"
Write-Host "Total de líneas: $($lines.Count)"
