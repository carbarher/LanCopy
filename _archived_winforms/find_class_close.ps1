$lines = Get-Content "MainForm.cs"
$balance = 0
$classStart = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1
    
    # Detectar inicio de clase
    if ($line -match "public partial class MainForm") {
        $classStart = $lineNum
        Write-Host "Clase MainForm encontrada en linea $lineNum"
        $balance = 0
    }
    
    if ($classStart -gt 0) {
        # Contar llaves
        $opens = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
        $closes = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
        
        $balance += $opens - $closes
        
        # Si el balance vuelve a 0, la clase se cerro
        if ($balance -eq 0 -and $lineNum -gt ($classStart + 1)) {
            Write-Host "CLASE SE CIERRA en linea $lineNum : $line"
            Write-Host "Siguiente linea ($($lineNum + 1)): $($lines[$i + 1])"
            
            # Verificar si hay mas codigo despues
            if ($lineNum -lt 21089) {
                Write-Host "ERROR: La clase se cerro ANTES de la linea 21089" -ForegroundColor Red
                Write-Host "Hay $($lines.Count - $lineNum) lineas despues del cierre"
                break
            }
        }
    }
}
