# Eliminar código suelto (líneas 928-1502) que quedó del método duplicado
$file = "MainForm.cs"
$content = Get-Content $file

# Backup
$backupName = "$file.backup2_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $file $backupName
Write-Host "Backup creado: $backupName" -ForegroundColor Gray

# Eliminar líneas 928-1502 (575 líneas de código suelto)
$newContent = @()
$lineNum = 0

foreach ($line in $content) {
    $lineNum++
    
    # Mantener líneas antes de 928 y después de 1502
    if ($lineNum -lt 928 -or $lineNum -gt 1502) {
        $newContent += $line
    } else {
        # Saltar estas líneas
        if ($lineNum -eq 928) {
            Write-Host "Eliminando líneas 928-1502 (código suelto)..." -ForegroundColor Yellow
        }
    }
}

# Guardar
$newContent | Set-Content $file -Encoding UTF8

Write-Host "`n✅ Código suelto eliminado" -ForegroundColor Green
Write-Host "Líneas eliminadas: 928-1502 (575 líneas)" -ForegroundColor Cyan
Write-Host "Líneas restantes: $($newContent.Count)" -ForegroundColor Cyan
