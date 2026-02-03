$source = "c:\p2p\downloads\Otros_Idiomas"
$destination = "c:\p2p\downloads\NO_ES\Otros_Idiomas"

Write-Host "Copiando archivos de Otros_Idiomas a NO_ES/Otros_Idiomas..."

if (Test-Path $source) {
    $files = Get-ChildItem -Path $source -Recurse -File
    $totalFiles = $files.Count
    Write-Host "Total de archivos a copiar: $totalFiles"
    
    $count = 0
    foreach ($file in $files) {
        $count++
        $relativePath = $file.FullName.Substring($source.Length)
        $destFile = Join-Path $destination $relativePath
        $destDir = Split-Path $destFile -Parent
        
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        Copy-Item -Path $file.FullName -Destination $destFile -Force
        
        if ($count % 100 -eq 0) {
            Write-Host "Copiados $count de $totalFiles archivos..."
        }
    }
    
    Write-Host "Copia completada: $count archivos copiados"
}
else {
    Write-Host "La carpeta origen no existe"
}
