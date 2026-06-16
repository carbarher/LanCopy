<#
.SYNOPSIS
  Publica LanCopy para todas las plataformas (o una seleccionada).
.EXAMPLE
  .\scripts\build-all.ps1               # todas las plataformas
  .\scripts\build-all.ps1 -Rid linux-x64
  .\scripts\build-all.ps1 -Version 1.2.0
#>
param(
    [string]$Rid = '',
    [string]$Version = '1.0.0',
    [string]$OutDir = 'publish'
)

$rids = if($Rid){ @($Rid) } else {
    @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')
}

foreach($r in $rids){
    Write-Host "`n==> $r" -ForegroundColor Cyan
    $out = Join-Path $OutDir $r
    dotnet publish LanCopy.csproj -c Release -r $r --self-contained true `
        -p:PublishSingleFile=true -p:Version=$Version -o $out --nologo
    if($LASTEXITCODE -ne 0){ Write-Error "$r FAILED"; exit 1 }
    Write-Host "$r OK -> $out" -ForegroundColor Green
}
Write-Host "`nTodos los RIDs publicados en '$OutDir'." -ForegroundColor Green