$lines = Get-Content "MainForm.cs"
$balance = 0
$maxBalance = 0
$problemLine = 0

for ($i = 0; $i < $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Contar llaves (ignorando las que están en strings)
    $inString = $false
    $escaped = $false
    
    for ($j = 0; $j < $line.Length; $j++) {
        $char = $line[$j]
        
        if ($escaped) {
            $escaped = $false
            continue
        }
        
        if ($char -eq '\') {
            $escaped = $true
            continue
        }
        
        if ($char -eq '"' -and !$inString) {
            $inString = $true
            continue
        }
        
        if ($char -eq '"' -and $inString) {
            $inString = $false
            continue
        }
        
        if (!$inString) {
            if ($char -eq '{') {
                $balance++
                if ($balance > $maxBalance) {
                    $maxBalance = $balance
                }
            }
            elseif ($char -eq '}') {
                $balance--
                if ($balance < 0) {
                    Write-Host "❌ ERROR: Llave de cierre sin apertura en línea $lineNum" -ForegroundColor Red
                    Write-Host "   $line"
                    $problemLine = $lineNum
                    break
                }
            }
        }
    }
    
    if ($problemLine -gt 0) {
        break
    }
}

Write-Host "`n📊 RESUMEN:"
Write-Host "Balance final: $balance (debería ser 0)"
Write-Host "Balance máximo alcanzado: $maxBalance"
Write-Host "Total de líneas: $($lines.Count)"

if ($balance -gt 0) {
    Write-Host "`n❌ FALTAN $balance llave(s) de cierre" -ForegroundColor Red
}
elseif ($balance -lt 0) {
    Write-Host "`n❌ SOBRAN $([Math]::Abs($balance)) llave(s) de cierre" -ForegroundColor Red
}
else {
    Write-Host "`n✅ Las llaves están balanceadas" -ForegroundColor Green
}
