<#

.SYNOPSIS

  Compara la lista «Biblioteca perfecta» (100 títulos) con archivos .txt o un catálogo tipo «autor, título».



.PARAMETER PublicDomainOnly

  Solo tiene en cuenta obras marcadas como dominio público en -MetaPath (regla UE 70 años alineada con PublicDomainValidator).



.PARAMETER MetaPath

  TSV con columnas title, author, death_year, is_pd (1/0). Misma línea que biblioteca-perfecta-100.txt. Por defecto: scripts/data/biblioteca-perfecta-100-meta.tsv



.PARAMETER ListPath

  Archivo con un título por línea (# comentarios). Por defecto: scripts/data/biblioteca-perfecta-100.txt



.PARAMETER LibraryPath

  Carpeta donde buscar *.txt de forma recursiva. Si se omite, solo se usa -CatalogPath (si existe).



.PARAMETER CatalogPath

  Archivo de texto con líneas «Algo, Título» (como libros.txt). Se busca si el título aparece en la parte tras la primera coma.



.EXAMPLE

  .\check-biblioteca-perfecta-txt.ps1 -CatalogPath c:\p2p\libros.txt -PublicDomainOnly



.EXAMPLE

  .\check-biblioteca-perfecta-txt.ps1 -LibraryPath D:\ebooks\txt -PublicDomainOnly

#>

param(

  [switch]$PublicDomainOnly,

  [string]$MetaPath = (Join-Path $PSScriptRoot 'data\biblioteca-perfecta-100-meta.tsv'),

  [string]$ListPath = (Join-Path $PSScriptRoot 'data\biblioteca-perfecta-100.txt'),

  [string]$LibraryPath = '',

  [string]$CatalogPath = '',

  [string[]]$LibraryExtensions = @('.txt', '.epub', '.pdf', '.mobi', '.azw', '.azw3', '.fb2', '.djvu')

)



function Normalize-ForMatch([string]$s) {

  if ([string]::IsNullOrWhiteSpace($s)) { return '' }

  $t = $s.ToLowerInvariant()

  $t = [regex]::Replace($t, '\([^)]*\)', ' ')

  $t = $t.Normalize([Text.NormalizationForm]::FormD)

  $sb = New-Object System.Text.StringBuilder

  foreach ($ch in $t.ToCharArray()) {

    $cat = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)

    if ($cat -ne [Globalization.UnicodeCategory]::NonSpacingMark) { [void]$sb.Append($ch) }

  }

  $t = $sb.ToString().Normalize([Text.NormalizationForm]::FormC)

  $t = $t -replace '[^a-z0-9áéíóúñü]+', ' '

  return ($t.Trim() -replace '\s+', ' ')

}



function Title-CoreKeywords([string]$normalized) {

  $stop = @('el','la','los','las','un','una','de','del','y','en','al')

  $parts = $normalized -split '\s+' | Where-Object { $_ -and ($stop -notcontains $_) }

  if ($parts.Count -eq 0) { return $normalized }

  return ($parts -join ' ')

}



$titles = Get-Content -LiteralPath $ListPath -Encoding UTF8 |

  Where-Object { $_ -and ($_ -notmatch '^\s*#') } |

  ForEach-Object { $_.Trim() }



$metaRows = @()

if ($PublicDomainOnly) {

  if (-not (Test-Path -LiteralPath $MetaPath)) {

    throw "MetaPath no encontrado: $MetaPath"

  }

  $metaRows = Import-Csv -LiteralPath $MetaPath -Delimiter "`t" -Encoding UTF8

  if ($metaRows.Count -ne $titles.Count) {

    throw "biblioteca-perfecta-100-meta.tsv: $($metaRows.Count) filas; lista: $($titles.Count) títulos."

  }

  for ($i = 0; $i -lt $titles.Count; $i++) {

    $mt = $metaRows[$i].title.Trim()

    if (-not $titles[$i].Equals($mt, [StringComparison]::Ordinal)) {

      throw "Desalineación fila $($i + 1): lista='$($titles[$i])' meta='$mt'"

    }

  }

}



$libraryBlob = ''

if ($LibraryPath -and (Test-Path -LiteralPath $LibraryPath)) {

  $nameParts = [System.Collections.Generic.List[string]]::new()

  foreach ($ext in $LibraryExtensions) {

    $pat = '*' + $ext

    Get-ChildItem -LiteralPath $LibraryPath -Filter $pat -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {

      [void]$nameParts.Add([System.IO.Path]::GetFileNameWithoutExtension($_.Name))

    }

  }

  $libraryBlob = Normalize-ForMatch ($nameParts -join "`n")

}



$catalogTitles = @()

if ($CatalogPath -and (Test-Path -LiteralPath $CatalogPath)) {

  Get-Content -LiteralPath $CatalogPath -Encoding UTF8 | ForEach-Object {

    $line = $_.Trim()

    if (-not $line) { return }

    $comma = $line.IndexOf(',')

    if ($comma -gt 0) { $line = $line.Substring($comma + 1).Trim() }

    $catalogTitles += (Normalize-ForMatch $line)

  }

}



function Test-AllWordsPresent([string]$haystack, [string]$needleWords) {

  if ([string]::IsNullOrWhiteSpace($needleWords)) { return $false }

  $words = $needleWords -split '\s+' | Where-Object { $_.Length -ge 2 }

  if ($words.Count -eq 0) { return $false }

  foreach ($w in $words) {

    if ($haystack.IndexOf($w, [StringComparison]::OrdinalIgnoreCase) -lt 0) { return $false }

  }

  return $true

}



function Test-InCatalog([string]$normFull, [string]$core, [string[]]$catalogNormTitles) {

  foreach ($ct in $catalogNormTitles) {

    if ($ct.Contains($normFull)) { return $true }

    if ($core.Length -ge 5 -and $ct.Contains($core)) { return $true }

    if ($normFull.Length -le 6 -and $normFull.Length -ge 2 -and $ct -eq $normFull) { return $true }

    if ((Test-AllWordsPresent $ct $core) -and $core.Length -ge 5) { return $true }

  }

  return $false

}



$present = [System.Collections.Generic.List[string]]::new()

$missing = [System.Collections.Generic.List[string]]::new()

$skippedNonPd = 0



for ($idx = 0; $idx -lt $titles.Count; $idx++) {

  $raw = $titles[$idx]

  if ($PublicDomainOnly -and $metaRows.Count -gt 0) {

    $pd = 0

    try { $pd = [int]$metaRows[$idx].is_pd } catch { $pd = 0 }

    if ($pd -ne 1) {

      $skippedNonPd++

      continue

    }

  }



  $normFull = Normalize-ForMatch $raw

  $core = Title-CoreKeywords $normFull

  $ok = $false

  if ($catalogTitles.Count -gt 0) {

    if (Test-InCatalog $normFull $core $catalogTitles) { $ok = $true }

  }

  if (-not $ok -and $libraryBlob) {

    if ($libraryBlob.Contains($normFull) -or ($core.Length -ge 5 -and $libraryBlob.Contains($core))) {

      $ok = $true

    }

  }

  if ($ok) { $present.Add($raw) } else { $missing.Add($raw) }

}



Write-Host "Lista: $ListPath ($($titles.Count) entradas)"

if ($PublicDomainOnly) {

  Write-Host "Solo dominio publico (UE 70 anos): $($titles.Count - $skippedNonPd) titulos (excluidos no-PD: $skippedNonPd); Meta: $MetaPath"

}

if ($CatalogPath) { Write-Host "Catálogo: $CatalogPath" }

if ($LibraryPath) { Write-Host "Carpeta biblioteca (nombres de archivo): $LibraryPath" }

Write-Host ""

Write-Host "Coincidencias: $($present.Count)"

Write-Host "Sin coincidencia: $($missing.Count)"

if ($missing.Count -gt 0) {

  Write-Host ""
  Write-Host "--- Faltan (heuristica) ---"

  foreach ($m in $missing) { Write-Host $m }

}


