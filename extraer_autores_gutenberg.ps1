# Extrae autores únicos de gutenberg_metadata.csv y los guarda en autores_gutenberg.txt
# Uso: Ejecuta este script en PowerShell desde la carpeta donde está el CSV

$csv = 'gutenberg_metadata.csv'
$out = 'autores_gutenberg.txt'

Write-Host "Procesando $csv ..."

$authors = @{}

# Lee el archivo línea por línea para evitar problemas de memoria
$reader = [System.IO.StreamReader]::new($csv)
$header = $reader.ReadLine()
$columns = $header -split ','
$authorIdx = $columns.IndexOf('Authors')

while (($line = $reader.ReadLine()) -ne $null) {
    $fields = $line -split ',(?=(?:[^"]*"[^"]*")*[^"]*$)' # Split CSV respetando comillas
    if ($fields.Count -le $authorIdx) { continue }
    $cell = $fields[$authorIdx].Trim('"')
    if ([string]::IsNullOrWhiteSpace($cell)) { continue }
    foreach ($author in $cell -split ',') {
        $a = $author.Trim()
        if ($a -and -not $authors.ContainsKey($a)) {
            $authors[$a] = $true
        }
    }
}
$reader.Close()

$authors.Keys | Sort-Object | Set-Content $out -Encoding UTF8

Write-Host "Autores únicos guardados en $out. Total: $($authors.Count)"
