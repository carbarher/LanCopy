$lines = Get-Content "MainForm.cs"
$balance = 0
$globalBalance = 0
$methodName = ""
$methodStart = 0

for ($i = 25000; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Detectar inicio de método
    if ($line -match "^\s*(private|public|protected|internal).*\(.*\)\s*$") {
        if ($methodName -ne "" -and $balance -ne 0) {
            Write-Host "ADVERTENCIA: Metodo '$methodName' (linea $methodStart) termino con balance $balance" -ForegroundColor Yellow
        }
        $methodName = $line.Trim()
        $methodStart = $lineNum
        $balance = 0
        Write-Host "`nMetodo encontrado en linea $lineNum : $methodName"
    }
    
    # Contar llaves
    $opens = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closes = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    
    $balance += $opens - $closes
    $globalBalance += $opens - $closes
    
    # Mostrar líneas con desbalance sospechoso
    if ($balance -lt 0) {
        Write-Host "  ERROR en linea $lineNum (balance: $balance): $line" -ForegroundColor Red
    }
    
    # Si el balance vuelve a 0, el método terminó
    if ($balance -eq 0 -and $methodName -ne "" -and $lineNum -gt $methodStart) {
        Write-Host "  Metodo termina en linea $lineNum"
        $methodName = ""
    }
}

Write-Host "`n========================================="
Write-Host "Balance global desde linea 25000: $globalBalance"
if ($globalBalance -eq 0) {
    Write-Host "Balance correcto en esta seccion" -ForegroundColor Green
} else {
    Write-Host "PROBLEMA: Balance incorrecto ($globalBalance)" -ForegroundColor Red
}
