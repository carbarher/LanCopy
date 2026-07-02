$ErrorActionPreference = 'Stop'

$baseUrl = if ($env:LANCOPY_API_URL) { $env:LANCOPY_API_URL } else { 'http://127.0.0.1:3489' }
$token = $env:LANCOPY_API_TOKEN
$transferId = $args | Select-Object -First 1
$localFile = if ($env:LOCAL_FILE) { $env:LOCAL_FILE } else { 'C:\tmp\file.zip' }
$localDir = if ($env:LOCAL_DIR) { $env:LOCAL_DIR } else { 'C:\tmp\data' }
$target = if ($env:LANCOPY_TARGET) { $env:LANCOPY_TARGET } else { '192.168.1.50:8742' }

if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Set LANCOPY_API_TOKEN first.'
}

Write-Host '== health =='
Invoke-RestMethod -Uri "$baseUrl/api/v1/health" | ConvertTo-Json -Depth 8

Write-Host '== peers =='
Invoke-RestMethod -Uri "$baseUrl/api/v1/peers" -Headers @{ 'X-LanCopy-Token' = $token } | ConvertTo-Json -Depth 8

Write-Host '== openapi =='
Invoke-RestMethod -Uri "$baseUrl/api/v1/openapi.json" | ConvertTo-Json -Depth 6

Write-Host '== send =='
$sendBody = @{
    localPath = $localFile
    to        = $target
} | ConvertTo-Json
Invoke-RestMethod -Uri "$baseUrl/api/v1/transfers/send" -Method Post -Headers @{ 'X-LanCopy-Token' = $token } -ContentType 'application/json' -Body $sendBody | ConvertTo-Json -Depth 8

Write-Host '== sync =='
$syncBody = @{
    localDir   = $localDir
    to         = $target
    remoteRoot = 'backup'
} | ConvertTo-Json
Invoke-RestMethod -Uri "$baseUrl/api/v1/sync" -Method Post -Headers @{ 'X-LanCopy-Token' = $token } -ContentType 'application/json' -Body $syncBody | ConvertTo-Json -Depth 8

if (-not [string]::IsNullOrWhiteSpace($transferId)) {
    Write-Host "== status $transferId =="
    Invoke-RestMethod -Uri "$baseUrl/api/v1/transfers/$transferId" -Headers @{ 'X-LanCopy-Token' = $token } | ConvertTo-Json -Depth 8

    Write-Host "== cancel $transferId =="
    Invoke-RestMethod -Uri "$baseUrl/api/v1/transfers/$transferId/cancel" -Method Post -Headers @{ 'X-LanCopy-Token' = $token } | ConvertTo-Json -Depth 8

    Write-Host "== retry $transferId =="
    Invoke-RestMethod -Uri "$baseUrl/api/v1/transfers/$transferId/retry" -Method Post -Headers @{ 'X-LanCopy-Token' = $token } | ConvertTo-Json -Depth 8
}
