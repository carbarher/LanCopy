<#
.SYNOPSIS
  Generates an ECDSA P-256 key pair for LanCopy release manifest signing.
.DESCRIPTION
  Store the private key as GitHub secret LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM.
  Pin the public key in UpdateChecker.ReleaseManifestPublicKeyPem before enabling strict signed updates.
#>
param(
    [string]$OutDir = 'release-signing-key'
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$key = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $privatePem = $key.ExportECPrivateKeyPem()
    $publicPem = $key.ExportSubjectPublicKeyInfoPem()
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'lancopy-release-private.pem'), $privatePem, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'lancopy-release-public.pem'), $publicPem, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Private key: $(Join-Path $OutDir 'lancopy-release-private.pem')"
    Write-Host "Public key:  $(Join-Path $OutDir 'lancopy-release-public.pem')"
    Write-Host "Add the private key content as GitHub secret LANCOPY_RELEASE_SIGNING_PRIVATE_KEY_PEM."
}
finally {
    $key.Dispose()
}
