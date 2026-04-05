# Build + test (misma secuencia que CI). Uso: .\build.ps1  o  pwsh -File build.ps1
# Con la app abierta, evita bloqueo de DLL:  .\build.ps1 -ShadowTests
#   .\build.ps1 -ShadowTests -- -v n --filter "FullyQualifiedName~AppConfig"
param(
    [switch] $ShadowTests,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $PassThruArgs
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if ($ShadowTests) {
    Write-Host "Tests con salida temporal de SlskDownAvalonia (app puede seguir en marcha)..."
    & pwsh -NoProfile -File "SlskDownAvalonia\scripts\Build-TestsShadow.ps1" @PassThruArgs
    exit $LASTEXITCODE
}

Write-Host "dotnet build SlskDown.sln (Release)..."
dotnet build "SlskDown.sln" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet test SlskDownAvalonia\SlskDownAvalonia.Tests.csproj (Release)..."
dotnet test "SlskDownAvalonia\SlskDownAvalonia.Tests.csproj" -c Release --no-build -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "OK."
