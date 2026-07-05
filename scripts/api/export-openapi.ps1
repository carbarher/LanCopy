param(
    [string]$BaseUrl = "http://127.0.0.1:3489",
    [string]$OutFile = "docs/api/openapi.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$outPath = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $repoRoot $OutFile }
$outDir = Split-Path -Parent $outPath
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$uri = ($BaseUrl.TrimEnd('/')) + "/api/v1/openapi.json"
Invoke-WebRequest -Uri $uri -OutFile $outPath
Write-Host "OpenAPI written to $outPath"