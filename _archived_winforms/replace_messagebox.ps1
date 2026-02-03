# Script para reemplazar MessageBox.Show por DarkMessageBox.Show

$file = "MainForm.cs"
$content = Get-Content $file -Raw -Encoding UTF8

# Reemplazar todas las ocurrencias
$content = $content -replace 'MessageBox\.Show', 'DarkMessageBox.Show'

# Guardar
Set-Content $file -Value $content -NoNewline -Encoding UTF8

Write-Host "Reemplazo completado"
Write-Host "Verificando..."

# Contar ocurrencias restantes
$remaining = (Select-String -Path $file -Pattern 'MessageBox\.Show' -AllMatches).Matches.Count
if ($remaining -eq 0) {
    Write-Host "OK - Todos los MessageBox.Show fueron reemplazados" -ForegroundColor Green
} else {
    Write-Host "ADVERTENCIA - Quedan $remaining ocurrencias de MessageBox.Show" -ForegroundColor Yellow
}
