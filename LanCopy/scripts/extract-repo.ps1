<#
.SYNOPSIS
  Extrae LanCopy del monorepo privado a un directorio limpio listo para publicar.

.DESCRIPTION
  Copia los archivos del proyecto (sin bin/, obj/, publish/, tests/TestResults/,
  ni archivos del monorepo padre) a un directorio nuevo que puede usarse como
  repositorio publico independiente.

.EXAMPLE
  .\scripts\extract-repo.ps1 -Dest C:\lancopy-public
  cd C:\lancopy-public
  git init; git add .; git commit -m "Initial release v1.0.0"
  gh repo create carbarher/LanCopy --public --source=. --push
#>
param(
    [Parameter(Mandatory)][string]$Dest
)

$src = Split-Path $PSScriptRoot -Parent   # C:\p2p\LanCopy

if(Test-Path $Dest){
    $yn = Read-Host "'$Dest' ya existe. Borrar y recrear? (s/n)"
    if($yn -notmatch '^[sySY]'){ Write-Host "Cancelado."; exit 0 }
    Remove-Item $Dest -Recurse -Force
}
New-Item $Dest -ItemType Directory | Out-Null

# Exclusiones (relativas al dir del proyecto)
$exclude = @(
    'bin','obj','publish','TestResults','.vs',
    'scripts\extract-repo.ps1'   # no incluirse a si mismo en el repo publico
)

function Copy-Clean {
    param($SrcDir, $DstDir)
    New-Item $DstDir -ItemType Directory -Force | Out-Null
    foreach($item in Get-ChildItem $SrcDir){
        $rel = $item.Name
        if($rel -in $exclude){ continue }
        if($item.PSIsContainer){
            Copy-Clean $item.FullName (Join-Path $DstDir $rel)
        } else {
            Copy-Item $item.FullName (Join-Path $DstDir $rel)
        }
    }
}

Copy-Clean $src $Dest
Write-Host "Extraido en: $Dest" -ForegroundColor Green
Write-Host ""
Write-Host "Siguientes pasos:" -ForegroundColor Yellow
Write-Host "  cd `"$Dest`""
Write-Host "  git init"
Write-Host "  git add ."
Write-Host "  git commit -m `"Initial release v1.0.0`""
Write-Host "  gh repo create carbarher/LanCopy --public --source=. --remote=origin --push"