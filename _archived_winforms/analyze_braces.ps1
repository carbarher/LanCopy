$lines = Get-Content 'c:\p2p\SlskDown\MainForm.cs'
$balance = 0
$startLine = 19506
$endLine = 20435

Write-Host "Analyzing braces from line $startLine to $endLine"
Write-Host "=" * 80

for($i = $startLine; $i -le $endLine; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    $openCount = ($line -split '\{' | Measure-Object).Count - 1
    $closeCount = ($line -split '\}' | Measure-Object).Count - 1
    
    $prevBalance = $balance
    $balance += $openCount - $closeCount
    
    # Mostrar líneas con llaves o líneas clave
    if ($openCount -gt 0 -or $closeCount -gt 0 -or $lineNum -in @(19507,19508,19509,20393,20394,20407,20408,20428,20429,20430)) {
        $indent = "  " * [Math]::Max(0, $prevBalance)
        $change = ""
        if ($openCount -gt 0) { $change += "+$openCount" }
        if ($closeCount -gt 0) { 
            if ($change) { $change += " " }
            $change += "-$closeCount" 
        }
        
        $displayLine = $line.Trim()
        if ($displayLine.Length -gt 60) {
            $displayLine = $displayLine.Substring(0, 57) + "..."
        }
        
        Write-Host ("{0,5}: {1} [{2,2}] {3}" -f $lineNum, $indent, $balance, $displayLine)
    }
}

Write-Host ""
Write-Host "=" * 80
Write-Host "Final balance: $balance (should be 0)"
if ($balance -ne 0) {
    Write-Host "ERROR: Unbalanced braces detected!" -ForegroundColor Red
    if ($balance -gt 0) {
        Write-Host "Missing $balance closing brace(s)" -ForegroundColor Red
    } else {
        Write-Host "Extra $([Math]::Abs($balance)) closing brace(s)" -ForegroundColor Red
    }
}
