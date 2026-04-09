#Requires -Version 5.1
param(
  [string]$Root = 'C:\p2p\downloads'
)

$ebookExt = @('.epub', '.pdf', '.mobi', '.azw', '.azw3', '.fb2', '.djvu')
$txtSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

Write-Host "Indexando .txt en $Root ..."
Get-ChildItem -LiteralPath $Root -Filter '*.txt' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
  $k = $_.DirectoryName + '|' + [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
  [void]$txtSet.Add($k)
}
Write-Host "Claves distintas (carpeta + nombre base) con .txt: $($txtSet.Count)"

$missingFiles = 0
$missingKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

foreach ($ext in $ebookExt) {
  Write-Host "Revisando *$ext ..."
  Get-ChildItem -LiteralPath $Root -Filter ('*' + $ext) -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    $k = $_.DirectoryName + '|' + [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    if (-not $txtSet.Contains($k)) {
      $missingFiles++
      [void]$missingKeys.Add($k)
    }
  }
}

Write-Host ""
Write-Host "Archivos ebook ($($ebookExt -join ', ')) sin .txt homonimo en la misma carpeta: $missingFiles"
Write-Host "Titulos unicos (carpeta + nombre base) sin .txt: $($missingKeys.Count)"
