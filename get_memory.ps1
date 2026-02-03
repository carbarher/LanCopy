# Script para obtener información de memoria
$os = Get-CimInstance -ClassName Win32_OperatingSystem
$total = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
$free = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
$used = $total - $free

Write-Host "=== INFORMACIÓN DE MEMORIA RAM ==="
Write-Host "Total: $total GB"
Write-Host "Libre: $free GB"
Write-Host "Usada: $used GB"
Write-Host "Porcentaje libre: $([math]::Round(($free/$total)*100, 1))%"
Write-Host "Porcentaje usada: $([math]::Round(($used/$total)*100, 1))%"
