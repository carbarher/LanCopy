# Script para convertir botones y ListView a componentes modernos
$filePath = "c:\p2p\SlskDown\MainForm.cs"
$content = Get-Content $filePath -Raw

# Contador de cambios
$changes = 0

# Convertir Button simple a ModernButton (sin FlatAppearance)
$pattern1 = '= new Button\s*\{([^}]+?)Text = "([^"]+)",([^}]+?)BackColor = Color\.FromArgb\((\d+),\s*(\d+),\s*(\d+)\),([^}]+?)ForeColor = Color\.White,([^}]+?)FlatStyle = FlatStyle\.Flat,([^}]+?)Font = new Font\([^)]+\)([^}]*?)\}'
$replacement1 = '= new ModernButton { Text = "$2", BackColor = Color.FromArgb($4, $5, $6)$10 }'
if ($content -match $pattern1) {
    $content = $content -replace $pattern1, $replacement1
    $changes++
}

# Convertir Button con FlatAppearance.BorderSize
$pattern2 = '(var \w+ = new Button[^;]+;)\s*\w+\.FlatAppearance\.BorderSize = 0;'
$content = $content -replace $pattern2, { 
    $buttonDecl = $_.Groups[1].Value
    $buttonDecl -replace 'new Button', 'new ModernButton' -replace ', ForeColor = Color\.White', '' -replace ', FlatStyle = FlatStyle\.Flat', '' -replace ', Font = new Font\([^)]+\)', '' -replace ', Cursor = Cursors\.Hand', ''
}

# Convertir ListView simple a ModernListView
$pattern3 = '= new ListView\s*\{\s*([^}]+?)View = View\.Details,\s*FullRowSelect = true,\s*GridLines = true,\s*OwnerDraw = true,\s*VirtualMode = false,\s*BackColor = Color\.\w+,\s*ForeColor = Color\.\w+\s*\}'
$replacement3 = '= new ModernListView { VirtualMode = false }'
if ($content -match $pattern3) {
    $content = $content -replace $pattern3, $replacement3
    $changes++
}

# Guardar cambios
Set-Content $filePath $content -NoNewline

Write-Host "✅ Conversión completada: $changes patrones aplicados" -ForegroundColor Green
Write-Host "Revisa MainForm.cs para verificar los cambios" -ForegroundColor Yellow
