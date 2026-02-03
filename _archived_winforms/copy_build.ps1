Set-Location "c:\p2p\SlskDown"
$source = "c:\p2p\SlskDown\bin\Release\net8.0-windows"
$dest = "c:\p2p\SlskDown\bin\publish_hotfix12"

"Iniciando copia..." | Out-File -FilePath "copy_log.txt"
"Source: $source" | Out-File -FilePath "copy_log.txt" -Append
"Dest: $dest" | Out-File -FilePath "copy_log.txt" -Append

if (Test-Path $source) {
    "Source existe" | Out-File -FilePath "copy_log.txt" -Append
} else {
    "Source NO existe" | Out-File -FilePath "copy_log.txt" -Append
    exit 1
}

New-Item -ItemType Directory -Path $dest -Force | Out-Null
"Directorio creado" | Out-File -FilePath "copy_log.txt" -Append

Copy-Item -Path "$source\*" -Destination $dest -Recurse -Force
"Copia completada" | Out-File -FilePath "copy_log.txt" -Append

if (Test-Path "$dest\SlskDown.exe") {
    "SlskDown.exe encontrado en destino" | Out-File -FilePath "copy_log.txt" -Append
} else {
    "SlskDown.exe NO encontrado" | Out-File -FilePath "copy_log.txt" -Append
}
