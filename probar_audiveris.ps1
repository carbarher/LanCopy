param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$AudiverisExe,
    [string]$OutputRoot = "C:\p2p\_audiveris_probe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AudiverisExe {
    param([string]$Preferred)

    if ($Preferred -and (Test-Path $Preferred)) { return (Resolve-Path $Preferred).Path }

    $candidates = @(
        "C:\ProgramData\chocolatey\bin\audiveris.bat",
        "C:\ProgramData\chocolatey\bin\audiveris.exe",
        "C:\Program Files\Audiveris\Audiveris.exe",
        "C:\Program Files\Audiveris\audiveris.exe",
        "C:\Program Files\Audiveris\bin\audiveris.bat",
        "C:\Program Files\Audiveris\bin\Audiveris.bat",
        "C:\Program Files\Audiveris\bin\audiveris.exe",
        "C:\Program Files\Audiveris\bin\Audiveris.exe",
        "C:\Program Files (x86)\Audiveris\bin\audiveris.bat",
        "C:\Program Files (x86)\Audiveris\bin\Audiveris.bat",
        "C:\Program Files (x86)\Audiveris\bin\audiveris.exe",
        "C:\Program Files (x86)\Audiveris\bin\Audiveris.exe"
    )

    foreach ($p in $candidates) {
        if (Test-Path $p) { return (Resolve-Path $p).Path }
    }

    $pathCmd = Get-Command audiveris -ErrorAction SilentlyContinue
    if ($pathCmd) { return $pathCmd.Source }

    return $null
}

function Invoke-AudiverisCase {
    param(
        [string]$CaseName,
        [string]$Exe,
        [string]$InputFile,
        [string]$CaseRoot,
        [string[]]$ExtraArgs,
        [hashtable]$EnvOverrides,
        [int]$TimeoutSeconds = 900
    )

    $caseDir = Join-Path $CaseRoot $CaseName
    New-Item -ItemType Directory -Path $caseDir -Force | Out-Null

    $outputDir = Join-Path $caseDir "output"
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

    $stdoutPath = Join-Path $caseDir "stdout.log"
    $stderrPath = Join-Path $caseDir "stderr.log"
    $metaPath = Join-Path $caseDir "meta.txt"

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Exe
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $args = @("-batch", "-transcribe", "-export", "-output", $outputDir, $InputFile)
    if ($ExtraArgs) {
        $args = @("-batch") + $ExtraArgs + @("-transcribe", "-export", "-output", $outputDir, $InputFile)
    }

    $quotedArgs = $args | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }
    $psi.Arguments = ($quotedArgs -join ' ')

    if ($EnvOverrides) {
        foreach ($k in $EnvOverrides.Keys) {
            $psi.EnvironmentVariables[$k] = [string]$EnvOverrides[$k]
        }
    }

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi

    $started = $proc.Start()
    if (-not $started) { throw "No se pudo iniciar Audiveris en caso $CaseName" }

    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()

    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        try { $proc.Kill($true) } catch {}
        throw "Timeout en caso $CaseName (${TimeoutSeconds}s)"
    }

    [System.Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask))

    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result

    Set-Content -Path $stdoutPath -Value $stdout -Encoding UTF8
    Set-Content -Path $stderrPath -Value $stderr -Encoding UTF8

    $combined = ($stderr + "`n" + $stdout)
    $estimating = $combined -match "Estimating resolution as"
    $pageError = $combined -match "Error in reaching step PAGE"

    $produced = @(Get-ChildItem -Path $outputDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".mxl", ".xml", ".mscz", ".mscx" })

    $meta = @(
        "Case=$CaseName",
        "Exe=$Exe",
        "Input=$InputFile",
        "Args=$($args -join ' ')",
        "ExitCode=$($proc.ExitCode)",
        "EstimatingResolution=$estimating",
        "PageError=$pageError",
        "Outputs=$($produced.Count)",
        "Timestamp=$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    )
    Set-Content -Path $metaPath -Value $meta -Encoding UTF8

    [pscustomobject]@{
        Case = $CaseName
        ExitCode = $proc.ExitCode
        EstimatingResolution = $estimating
        PageError = $pageError
        Outputs = $produced.Count
        Meta = $metaPath
        StdErr = $stderrPath
        StdOut = $stdoutPath
    }
}

$resolvedInput = Resolve-Path $InputPath -ErrorAction Stop
$exe = Resolve-AudiverisExe -Preferred $AudiverisExe
if (-not $exe) {
    throw "No se encontro Audiveris. Pasa -AudiverisExe o instala Audiveris."
}

$session = Get-Date -Format "yyyyMMdd_HHmmss"
$caseRoot = Join-Path $OutputRoot $session
New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null

$isolatedAppData = Join-Path $caseRoot "appdata"
New-Item -ItemType Directory -Path (Join-Path $isolatedAppData "AudiverisLtd\audiveris\config") -Force | Out-Null
$runProps = Join-Path $isolatedAppData "AudiverisLtd\audiveris\config\run.properties"
Set-Content -Path $runProps -Encoding UTF8 -Value @(
    "org.audiveris.omr.image.ImageLoading.pdfResolution=300"
)

$cases = @(
    @{ Name = "baseline"; ExtraArgs = @(); Env = @{ JAVA_OPTS = "-Xmx4g" } },
    @{ Name = "constant_300"; ExtraArgs = @("-constant", "org.audiveris.omr.image.ImageLoading.pdfResolution=300"); Env = @{ JAVA_OPTS = "-Xmx4g" } },
    @{ Name = "constant_250"; ExtraArgs = @("-constant", "org.audiveris.omr.image.ImageLoading.pdfResolution=250"); Env = @{ JAVA_OPTS = "-Xmx4g" } },
    @{ Name = "isolated_appdata_300"; ExtraArgs = @(); Env = @{ JAVA_OPTS = "-Xmx4g"; APPDATA = $isolatedAppData } }
)

$results = @()
foreach ($c in $cases) {
    Write-Host "--- Running case: $($c.Name)"
    try {
        $res = Invoke-AudiverisCase -CaseName $c.Name -Exe $exe -InputFile $resolvedInput.Path -CaseRoot $caseRoot -ExtraArgs $c.ExtraArgs -EnvOverrides $c.Env
        $results += $res
        Write-Host ("Case={0} Exit={1} Estim={2} PageErr={3} Outputs={4}" -f $res.Case, $res.ExitCode, $res.EstimatingResolution, $res.PageError, $res.Outputs)
    }
    catch {
        Write-Host "Case=$($c.Name) ERROR: $($_.Exception.Message)"
    }
}

$summaryPath = Join-Path $caseRoot "summary.txt"
$lines = @("Input=$($resolvedInput.Path)", "Exe=$exe", "Session=$session", "")
$lines += $results | ForEach-Object { "Case=$($_.Case) Exit=$($_.ExitCode) Estim=$($_.EstimatingResolution) PageErr=$($_.PageError) Outputs=$($_.Outputs)" }
Set-Content -Path $summaryPath -Value $lines -Encoding UTF8

Write-Host ""
Write-Host "Summary: $summaryPath"
Write-Host "Session dir: $caseRoot"
