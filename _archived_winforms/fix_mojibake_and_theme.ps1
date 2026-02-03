param(
    [string]$TargetFile = "c:\p2p\SlskDown\MainForm.cs",
    [switch]$Build
)

if (-not (Test-Path $TargetFile)) {
    throw "No se encontró el archivo: $TargetFile"
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backup = "$TargetFile.bak_$timestamp"
Copy-Item $TargetFile $backup -Force
Write-Host "Backup creado:" $backup

$content = [System.IO.File]::ReadAllText($TargetFile, [System.Text.Encoding]::UTF8)

function Convert-ToAscii {
    param([string]$Text)

    $normalized = $Text.Normalize([System.Text.NormalizationForm]::FormD)
    $builder = New-Object System.Text.StringBuilder

    foreach ($ch in $normalized.ToCharArray()) {
        $category = [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)
        if ($category -eq [System.Globalization.UnicodeCategory]::NonSpacingMark) { continue }

        $code = [int][char]$ch
        switch ($code) {
            { $_ -le 127 } { $null = $builder.Append([char]$code); continue }
            0x2018 { $null = $builder.Append("'"); continue }
            0x2019 { $null = $builder.Append("'"); continue }
            0x201C { $null = $builder.Append('"'); continue }
            0x201D { $null = $builder.Append('"'); continue }
            0x2013 { $null = $builder.Append('-'); continue }
            0x2014 { $null = $builder.Append('-'); continue }
            default { continue }
        }
    }

    return $builder.ToString()
}

$content = Convert-ToAscii $content
$content = [Regex]::Replace($content, '"\s+([A-Za-z0-9])', '"$1')

$colorMap = @{
    'Color.FromArgb(18, 18, 18)' = 'Color.FromArgb(0, 40, 0)';
    'Color.FromArgb(25, 25, 25)' = 'Color.FromArgb(0, 48, 0)';
    'Color.FromArgb(35, 35, 35)' = 'Color.FromArgb(0, 64, 0)';
    'Color.FromArgb(30, 30, 30)' = 'Color.FromArgb(0, 56, 0)';
    'Color.FromArgb(45, 45, 45)' = 'Color.FromArgb(0, 70, 0)';
    'Color.FromArgb(50, 50, 50)' = 'Color.FromArgb(0, 76, 0)';
    'Color.FromArgb(60, 60, 60)' = 'Color.FromArgb(0, 92, 0)';
    'Color.Black'               = 'Color.FromArgb(0, 24, 0)';
    'BackColor = Color.FromArgb(0, 120, 215)' = 'BackColor = Color.FromArgb(0, 140, 0)';
    'BackColor = Color.FromArgb(0, 150, 0)'   = 'BackColor = Color.FromArgb(0, 160, 0)';
    'BackColor = Color.FromArgb(180, 0, 0)'   = 'BackColor = Color.FromArgb(60, 0, 0)'
}

foreach ($entry in $colorMap.GetEnumerator()) {
    $content = $content.Replace($entry.Key, $entry.Value)
}

$content = $content.Replace('ForeColor = Color.White', 'ForeColor = Color.FromArgb(210, 255, 210)')
$content = $content.Replace('ForeColor = Color.White,', 'ForeColor = Color.FromArgb(210, 255, 210),')
$content = $content.Replace('ForeColor = Color.LightGray', 'ForeColor = Color.FromArgb(180, 220, 180)')

$fixStrings = @{
    '" Búsqueda"'   = '"Busqueda"';
    '" Descargas"'  = '"Descargas"';
    '" Configuración"' = '"Configuracion"';
    '" Lista Negra"'   = '"Lista Negra"';
    '" Autores"'    = '"Autores"';
    '" Watchlist"'  = '"Watchlist"';
    '" Automático"' = '"Automatico"';
    '" Log"'        = '"Log"';
    '" Conectar"'   = '"Conectar"';
    '" Buscar"'     = '"Buscar"';
    '" Detener"'    = '"Detener"';
    '" Español"'    = '"Espanol"';
    '" Limpiar"'    = '"Limpiar"';
    '" Carpeta"'    = '"Carpeta"';
    '" Purga"'      = '"Purga"'
}

foreach ($entry in $fixStrings.GetEnumerator()) {
    $content = $content.Replace($entry.Key, $entry.Value)
}

$themeBlock = @"
        private void ApplyDarkTheme()
        {
            // Configurar colores globales para el formulario
            this.BackColor = Color.FromArgb(0, 32, 0);
            this.ForeColor = Color.FromArgb(210, 255, 210);
        }
"@

$content = [Regex]::Replace(
    $content,
    'private void ApplyDarkTheme\(\)\s*\{.*?\n\s*\}',
    [Regex]::Escape($themeBlock).Replace('\r', '\\r').Replace('\n', '\\n') -replace '\\\\', '\',
    [RegexOptions]::Singleline
)

[System.IO.File]::WriteAllText($TargetFile, $content, [System.Text.UTF8Encoding]::new($true))
Write-Host "Archivo actualizado correctamente." -ForegroundColor Green

if ($Build) {
    Write-Host "Compilando proyecto..." -ForegroundColor Cyan
    $projectDir = Split-Path $TargetFile
    Push-Location $projectDir
    dotnet clean  | Out-Null
    dotnet build SlskDown.csproj -c Release | Out-Null
    Pop-Location
    Write-Host "Compilación finalizada." -ForegroundColor Green
}
