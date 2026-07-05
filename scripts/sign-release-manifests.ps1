<#
.SYNOPSIS
  Generates LanCopy release manifests beside release assets.
.DESCRIPTION
  Without a signing key, writes legacy .sha256 files containing only the SHA-256 hash.
  With an ECDSA P-256 private key, writes JSON .sha256 manifests with sha256 and signature.

  Signature payload must match UpdateChecker:
    assetName + newline + sha256 + newline
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$AssetsDir,

    [string]$PrivateKeyPemPath = '',
    [string]$PrivateKeyPem = $env:LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AssetsDir -PathType Container)) {
    throw "AssetsDir does not exist: $AssetsDir"
}

if ($PrivateKeyPemPath) {
    $PrivateKeyPem = Get-Content -LiteralPath $PrivateKeyPemPath -Raw
}

$ecdsa = $null
if (-not [string]::IsNullOrWhiteSpace($PrivateKeyPem)) {
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create()
    $ecdsa.ImportFromPem($PrivateKeyPem)
}

try {
    $assets = Get-ChildItem -LiteralPath $AssetsDir -File |
        Where-Object {
            $_.Name -notlike '*.sha256' -and
            $_.Name -notlike '*.sig' -and
            $_.Name -ne 'checksums.txt'
        } |
        Sort-Object Name

    foreach ($asset in $assets) {
        $bytes = [System.IO.File]::ReadAllBytes($asset.FullName)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha.ComputeHash($bytes)
        }
        finally {
            $sha.Dispose()
        }
        $sha256 = (($hashBytes | ForEach-Object { $_.ToString("x2") }) -join "")
        $manifestPath = $asset.FullName + '.sha256'

        if ($null -eq $ecdsa) {
            [System.IO.File]::WriteAllText($manifestPath, $sha256 + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
            Write-Host "manifest legacy $($asset.Name).sha256"
            continue
        }

        $payload = [System.Text.Encoding]::UTF8.GetBytes($asset.Name + "`n" + $sha256 + "`n")
        $signature = [Convert]::ToBase64String($ecdsa.SignData($payload, [System.Security.Cryptography.HashAlgorithmName]::SHA256))
        $json = [ordered]@{
            sha256 = $sha256
            signature = $signature
        } | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
        Write-Host "manifest signed $($asset.Name).sha256"
    }

    $checksumLines = Get-ChildItem -LiteralPath $AssetsDir -File -Filter '*.sha256' |
        Sort-Object Name |
        ForEach-Object { "$($_.Name)" }
    [System.IO.File]::WriteAllText((Join-Path $AssetsDir 'checksums.txt'), ($checksumLines -join [Environment]::NewLine) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}
finally {
    if ($null -ne $ecdsa) { $ecdsa.Dispose() }
}
