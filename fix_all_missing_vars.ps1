# Script para comentar TODAS las líneas que usan variables no declaradas

$filePath = "c:\p2p\SlskDown\MainForm.cs"

Write-Host "Arreglando MainForm.cs..." -ForegroundColor Cyan

# Backup
$backup = "$filePath.backup_fix_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $filePath $backup -Force
Write-Host "Backup: $backup" -ForegroundColor Green

# Leer líneas
$lines = [System.IO.File]::ReadAllLines($filePath, [System.Text.Encoding]::UTF8)

# Variables y métodos problemáticos
$patterns = @(
    'authorsUpdater',
    'duplicateAuthorGroups',
    'duplicateAuthorTotalCount',
    'BookMetadataViewer',
    'AutoSearchState',
    'SlskDownCore\.CalculateSimilarity',
    'RustCore\.FindPatterns',
    'RustCore\.RegexMatch',
    'RustCore\.BloomContains',
    'RustCore\.Crc32',
    'RustCore\.Tokenize',
    'RustCore\.UrlEncode',
    '\.Synopsis'
)

$commented = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Skip si ya está comentada
    if ($line.TrimStart() -match '^\s*//') {
        continue
    }
    
    # Verificar si contiene algún patrón
    $shouldComment = $false
    foreach ($pattern in $patterns) {
        if ($line -match $pattern) {
            $shouldComment = $true
            break
        }
    }
    
    if ($shouldComment) {
        # Obtener indentación y comentar
        if ($line -match '^(\s*)(.+)$') {
            $indent = $matches[1]
            $content = $matches[2]
            $lines[$i] = "$indent// $content"
            $commented++
        }
    }
}

Write-Host "Líneas comentadas: $commented" -ForegroundColor Yellow

# Guardar
[System.IO.File]::WriteAllLines($filePath, $lines, [System.Text.Encoding]::UTF8)

Write-Host "Guardado OK" -ForegroundColor Green
Write-Host ""
Write-Host "Compilando..." -ForegroundColor Cyan

# Compilar
cd c:\p2p\SlskDown
$result = dotnet build SlskDown.csproj -c Release 2>&1 | Out-String

if ($result -match "Build succeeded") {
    Write-Host "COMPILACION EXITOSA!" -ForegroundColor Green
} elseif ($result -match "(\d+) Error") {
    $errorCount = $matches[1]
    Write-Host "Aun quedan $errorCount errores" -ForegroundColor Red
    $result -split "`n" | Select-String "error CS" | Select-Object -First 10
} else {
    Write-Host "Estado desconocido" -ForegroundColor Yellow
}
