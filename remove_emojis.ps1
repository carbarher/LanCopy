# Script para eliminar todos los emojis de archivos .cs en SlskDown
# Elimina caracteres Unicode en rangos de emojis comunes

$projectPath = "c:\p2p\SlskDown"
$encoding = [System.Text.UTF8Encoding]::new($false) # UTF-8 sin BOM

# Rangos Unicode de emojis más comunes
$emojiPattern = '[' +
    '\u{1F300}-\u{1F9FF}' +  # Emojis misceláneos y símbolos
    '\u{2600}-\u{26FF}' +     # Símbolos misceláneos
    '\u{2700}-\u{27BF}' +     # Dingbats
    '\u{1F600}-\u{1F64F}' +   # Emoticones
    '\u{1F680}-\u{1F6FF}' +   # Símbolos de transporte y mapas
    '\u{1F1E0}-\u{1F1FF}' +   # Banderas
    '\u{2300}-\u{23FF}' +     # Símbolos técnicos misceláneos
    '\u{2B50}' +              # Estrella
    '\u{2B55}' +              # Círculo
    '\u{FE0F}' +              # Selector de variación
    '\u{200D}' +              # Zero Width Joiner
    ']'

$filesProcessed = 0
$emojisRemoved = 0

Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = [System.IO.File]::ReadAllText($file.FullName, $encoding)
    $originalLength = $content.Length
    
    # Eliminar emojis
    $newContent = $content -replace $emojiPattern, ''
    
    if ($newContent.Length -ne $originalLength) {
        $removed = $originalLength - $newContent.Length
        $emojisRemoved += $removed
        [System.IO.File]::WriteAllText($file.FullName, $newContent, $encoding)
        Write-Host "Procesado: $($file.Name) - $removed caracteres eliminados"
        $filesProcessed++
    }
}

Write-Host ""
Write-Host "Resumen:"
Write-Host "Archivos procesados: $filesProcessed"
Write-Host "Caracteres emoji eliminados: $emojisRemoved"
