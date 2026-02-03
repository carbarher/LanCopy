param(
    [string]$Path
)

if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Join-Path $PSScriptRoot "..\Data\canonical_authors_priority.txt"
}

$resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
$lines = Get-Content -LiteralPath $resolved -ErrorAction Stop

$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$out = New-Object 'System.Collections.Generic.List[string]'

foreach ($line in $lines) {
    $t = (($line -replace '[\p{Cf}\p{Cc}]', '') -replace '\s+', ' ').Trim()
    if ($t.Length -eq 0) {
        continue
    }

    $display = $t.Normalize([System.Text.NormalizationForm]::FormKC)

    $key = $display.Normalize([System.Text.NormalizationForm]::FormKD)
    $key = ($key -replace '\p{Mn}', '')
    $key = ($key -replace '[\p{Cf}\p{Cc}]', '')
    $key = ($key -replace '[^\p{L}\p{Nd}]', '')
    $key = $key.ToLowerInvariant()

    if ($seen.Add($key)) {
        $out.Add($display.Normalize([System.Text.NormalizationForm]::FormC))
    }
}

$tmp = "$($resolved.Path).tmp"
[System.IO.File]::WriteAllLines($tmp, $out.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
Move-Item -LiteralPath $tmp -Destination $resolved.Path -Force
