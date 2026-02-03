$source = "MainForm.cs.backup_full"
$dest = "MainForm.cs"
$backup = "MainForm.cs.old_version_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host "Verificando archivos..."
Write-Host "Source size: $((Get-Item $source).Length) bytes"
Write-Host "Dest size: $((Get-Item $dest).Length) bytes"

Write-Host "`nCreando backup del archivo actual..."
Copy-Item $dest $backup -Force

Write-Host "Restaurando backup_full..."
Copy-Item $source $dest -Force

Write-Host "`nVerificando restauración..."
Write-Host "New dest size: $((Get-Item $dest).Length) bytes"

if ((Get-Item $dest).Length -eq (Get-Item $source).Length) {
    Write-Host "`n✅ RESTAURACIÓN EXITOSA!" -ForegroundColor Green
} else {
    Write-Host "`n❌ ERROR: Los tamaños no coinciden" -ForegroundColor Red
}
