param(
    [string]$InputPath = "C:\p2p\_tmp_musescore_video_test.musicxml",
    [switch]$BuildScoreDown
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return Split-Path -Parent $PSScriptRoot
}

function Resolve-Ffprobe {
    $candidates = @(
        'C:\ffmpeg\bin\ffprobe.exe',
        'C:\ProgramData\chocolatey\bin\ffprobe.exe',
        'C:\Program Files\ffmpeg\bin\ffprobe.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $cmd = Get-Command ffprobe.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) {
        return $cmd.Source
    }

    return $null
}

function Invoke-ScoreDownBuild {
    param([string]$RepoRoot)

    $retried = $false
    $buildOutput = & dotnet build (Join-Path $RepoRoot 'ScoreDown\ScoreDown.csproj') -nologo 2>&1 | ForEach-Object { $_.ToString() }
    $code = $LASTEXITCODE
    $lockPattern = '(?i)(apphost\.exe|ScoreDown\.exe).*(being used by another process|cannot access the file|used by another process|The process cannot access the file)|(?i)(being used by another process|cannot access the file|used by another process|The process cannot access the file).*(apphost\.exe|ScoreDown\.exe)'

    if ($code -ne 0 -and ($buildOutput -join "`n") -match $lockPattern) {
        $retried = $true
        Get-Process ScoreDown -ErrorAction SilentlyContinue | Stop-Process -Force
        $buildOutput = & dotnet build (Join-Path $RepoRoot 'ScoreDown\ScoreDown.csproj') -nologo 2>&1 | ForEach-Object { $_.ToString() }
        $code = $LASTEXITCODE
    }

    $keyLines = $buildOutput | Where-Object {
        $_ -match '(^.*error .*CS\d+.*$)|(^.*MSB\d+.*$)|(^.*Build FAILED.*$)|(^.*Restore failed.*$)|(^.*Restaurar erróneo.*$)|(^.*cannot access the file.*$)|(^.*used by another process.*$)'
    } | Select-Object -Unique -First 20

    [pscustomobject]@{
        Retried = $retried
        Result = if ($code -eq 0) { 'SUCCESS' } else { 'FAIL' }
        ExitCode = $code
        KeyLines = $keyLines
    }
}

function Get-ProbeInfo {
    param(
        [string]$FfprobeExe,
        [string]$VideoPath
    )

    if (-not $FfprobeExe -or -not (Test-Path $VideoPath)) {
        return [pscustomobject]@{
            Duration = $null
            Resolution = $null
            Audio = 'unknown'
        }
    }

    $json = & $FfprobeExe -v error -show_streams -show_format -of json $VideoPath 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return [pscustomobject]@{
            Ready = $false
            Duration = $null
            Resolution = $null
            Audio = 'unknown'
        }
    }

    $obj = $json | ConvertFrom-Json
    $video = @($obj.streams | Where-Object { $_.codec_type -eq 'video' }) | Select-Object -First 1
    $audio = @($obj.streams | Where-Object { $_.codec_type -eq 'audio' }) | Select-Object -First 1

    $duration = $null
    if ($obj.format.duration) {
        try { $duration = [Math]::Round([double]$obj.format.duration, 2) } catch {}
    }
    if ($null -eq $duration -and $video -and $video.duration) {
        try { $duration = [Math]::Round([double]$video.duration, 2) } catch {}
    }

    $resolution = $null
    if ($video -and $video.width -and $video.height) {
        $resolution = "$($video.width)x$($video.height)"
    }

    [pscustomobject]@{
        Ready = $true
        Duration = $duration
        Resolution = $resolution
        Audio = if ($audio) { 'yes' } else { 'no' }
    }
}

function Wait-ForStableVideoProbe {
    param(
        [string]$FfprobeExe,
        [string]$VideoPath,
        [int]$MaxWaitSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($MaxWaitSeconds)
    $lastSize = -1L
    $stableTicks = 0

    while ((Get-Date) -lt $deadline) {
        if (-not (Test-Path $VideoPath)) {
            Start-Sleep -Seconds 1
            continue
        }

        $size = (Get-Item $VideoPath).Length
        if ($size -gt 0 -and $size -eq $lastSize) {
            $stableTicks++
        }
        else {
            $stableTicks = 0
            $lastSize = $size
        }

        $probe = Get-ProbeInfo -FfprobeExe $FfprobeExe -VideoPath $VideoPath
        if ($probe.Ready -and $stableTicks -ge 1) {
            return $probe
        }

        Start-Sleep -Seconds 1
    }

    return Get-ProbeInfo -FfprobeExe $FfprobeExe -VideoPath $VideoPath
}

function Invoke-MuseScoreCase {
    param(
        [string]$Label,
        [string]$ExePath,
        [string]$InputFile,
        [string]$OutputFile,
        [string]$FfprobeExe
    )

    $stdout = "$OutputFile.stdout.txt"
    $stderr = "$OutputFile.stderr.txt"
    Remove-Item $OutputFile, $stdout, $stderr -Force -ErrorAction SilentlyContinue

    $args = @('--score-video', '--resolution', '1080p', '--fps', '30', '--sound-profile', 'MuseSounds', '-o', $OutputFile, $InputFile)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $ExePath -ArgumentList $args -PassThru -Wait -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $sw.Stop()

    $exists = Test-Path $OutputFile
    $size = if ($exists) { (Get-Item $OutputFile).Length } else { 0 }
    $probe = Wait-ForStableVideoProbe -FfprobeExe $FfprobeExe -VideoPath $OutputFile
    $stderrLines = if (Test-Path $stderr) {
        Get-Content $stderr | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 12
    }
    else {
        @()
    }

    [pscustomobject]@{
        Label = $Label
        ExePath = $ExePath
        ElapsedSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        ExitCode = $proc.ExitCode
        Mp4Exists = $exists
        SizeBytes = $size
        ProbeReady = $probe.Ready
        DurationSeconds = $probe.Duration
        Resolution = $probe.Resolution
        Audio = $probe.Audio
        Stderr = $stderrLines
    }
}

$repoRoot = Get-RepoRoot

if (-not (Test-Path $InputPath)) {
    throw "No existe archivo de entrada: $InputPath"
}

if ($BuildScoreDown) {
    $build = Invoke-ScoreDownBuild -RepoRoot $repoRoot
    "BUILD_RESULT=$($build.Result)"
    "BUILD_RETRIED=$($build.Retried)"
    "BUILD_EXIT_CODE=$($build.ExitCode)"
    if ($build.KeyLines) {
        'BUILD_KEY_LINES_START'
        $build.KeyLines
        'BUILD_KEY_LINES_END'
    }
}

$ffprobeExe = Resolve-Ffprobe
"FFPROBE=$ffprobeExe"

$outputStem = [System.IO.Path]::GetFileNameWithoutExtension($InputPath)

$cases = @(
    @{ Label = 'testing-4.7.0'; Exe = 'C:\Program Files\MuseScore 4 Testing\bin\MuseScore4.exe'; Output = Join-Path $repoRoot ($outputStem + '.testing.mp4') },
    @{ Label = 'stable-4.7.1'; Exe = 'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'; Output = Join-Path $repoRoot ($outputStem + '.stable.mp4') }
)

foreach ($case in $cases) {
    if (-not (Test-Path $case.Exe)) {
        "CASE=$($case.Label)"
        "EXE_MISSING=True"
        continue
    }

    $result = Invoke-MuseScoreCase -Label $case.Label -ExePath $case.Exe -InputFile $InputPath -OutputFile $case.Output -FfprobeExe $ffprobeExe
    "CASE=$($result.Label)"
    "ELAPSED_SECONDS=$($result.ElapsedSeconds)"
    "EXIT_CODE=$($result.ExitCode)"
    "MP4_EXISTS=$($result.Mp4Exists)"
    "SIZE_BYTES=$($result.SizeBytes)"
    "PROBE_READY=$($result.ProbeReady)"
    "DURATION_SECONDS=$($result.DurationSeconds)"
    "RESOLUTION=$($result.Resolution)"
    "AUDIO=$($result.Audio)"
    if ($result.Stderr -and $result.Stderr.Count -gt 0) {
        'STDERR_START'
        $result.Stderr
        'STDERR_END'
    }
}