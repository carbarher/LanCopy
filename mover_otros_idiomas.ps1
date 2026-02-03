$source = "c:\p2p\downloads\Otros_Idiomas"
$destination = "c:\p2p\downloads\_NoEspanol\Otros_Idiomas"

Write-Host "Moviendo carpeta Otros_Idiomas a _NoEspanol..."
Write-Host "Origen: $source"
Write-Host "Destino: $destination"

if (Test-Path $source) {
    Write-Host "Carpeta origen existe"
    
    # Crear carpeta destino si no existe
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Write-Host "Carpeta destino creada"
    }
    
    # Mover la carpeta
    try {
        Move-Item -Path $source -Destination "c:\p2p\downloads\_NoEspanol\" -Force
        Write-Host "Carpeta movida exitosamente"
    }
    catch {
        Write-Host "Error al mover: $_"
    }
}
else {
    Write-Host "La carpeta origen no existe"
}

Write-Host "Operacion completada"
