# Script para comentar bloques de código problemático

$filePath = "c:\p2p\SlskDown\MainForm.cs"

Write-Host "Comentando código problemático en MainForm.cs..." -ForegroundColor Cyan

# Crear backup
$backup = "$filePath.backup_comment_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $filePath $backup -Force
Write-Host "[OK] Backup: $backup" -ForegroundColor Green

# Leer contenido
$lines = Get-Content $filePath -Encoding UTF8

# Patrones de líneas a comentar (si no están ya comentadas)
$patterns = @(
    'authorSearchIndex',
    'duplicateAuthorGroups',
    'authorsUpdater',
    'IndexAuthorsForSearch',
    'SearchAuthorIntelligentSilent',
    'BookMetadataViewer',
    'AutoSearchState',
    'SlskDownCore.CalculateSimilarity',
    'RustSearchIndex.IsRustAvailable',
    'RustCore.FindPatterns',
    'RustCore.RegexMatch',
    'RustCore.BloomContains',
    'RustCore.Crc32',
    'RustCore.Tokenize',
    'RustCore.UrlEncode',
    'metadata.Synopsis',
    'duplicateAuthorTotalCount'
)

$commented = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Skip si ya está comentada
    if ($line.TrimStart() -match '^\s*//') {
        continue
    }
    
    # Verificar si contiene algún patrón problemático
    $shouldComment = $false
    foreach ($pattern in $patterns) {
        if ($line -match [regex]::Escape($pattern)) {
            $shouldComment = $true
            break
        }
    }
    
    if ($shouldComment) {
        # Obtener indentación
        if ($line -match '^(\s*)(.*)$') {
            $indent = $matches[1]
            $content = $matches[2]
            $lines[$i] = "$indent// $content"
            $commented++
        }
    }
}

Write-Host "Líneas comentadas: $commented" -ForegroundColor Yellow

# Guardar
$lines | Set-Content $filePath -Encoding UTF8 -Force

Write-Host "[OK] Archivo guardado" -ForegroundColor Green
Write-Host ""
Write-Host "Ejecuta: dotnet build SlskDown.csproj -c Release" -ForegroundColor Cyan
