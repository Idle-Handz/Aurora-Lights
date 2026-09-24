param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project = Join-Path $PSScriptRoot 'ContentDatabaseRehearsal.csproj'
if (-not $NoBuild) {
    & dotnet build $project -c Release -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Rehearsal build failed.' }
}
$assembly = Join-Path $PSScriptRoot 'bin/Release/net10.0/ContentDatabaseRehearsal.dll'
$runRoot = Join-Path $repoRoot ('buildtmp/content-policy-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$phases = @('policy-first-import','policy-reopen-unavailable','policy-repair-conflict','policy-reopen-repaired',
    'policy-reject-conflict','policy-repair-conflict','policy-protect-correction','policy-reject-correction',
    'policy-probe-invalid-startup','policy-repair-correction','policy-reopen-repaired')
$results = [Collections.Generic.List[object]]::new()
$previousOutput = $env:REHEARSAL_OUTPUT
try {
    foreach ($skip in @($false,$true)) {
        $case = Join-Path $runRoot ('skip-' + $skip.ToString().ToLowerInvariant())
        New-Item -ItemType Directory -Path $case | Out-Null
        Set-Content -LiteralPath (Join-Path $case '.aurora-rehearsal') -Value 'Disposable policy rehearsal'
        Set-Content -LiteralPath (Join-Path $case 'policy-skip.txt') -Value $skip.ToString()
        $index = 0
        foreach ($phase in $phases) {
            $index++
            $phaseOutput = Join-Path $case ('{0:D2}-{1}' -f $index,$phase)
            New-Item -ItemType Directory -Path $phaseOutput | Out-Null
            $env:REHEARSAL_OUTPUT = $phaseOutput
            & dotnet $assembly $phase $case > (Join-Path $phaseOutput 'process-output.log') 2>&1
            $phaseExit = $LASTEXITCODE
            $reportPath = Join-Path $phaseOutput ($phase + '-result.json')
            if (-not (Test-Path -LiteralPath $reportPath)) { throw "Missing report: $phaseOutput (exit $phaseExit)" }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $results.Add([ordered]@{ skipUnusable=$skip; phase=$phase; exit=$phaseExit; success=$report.success;
                processId=$report.result.processId; checks=$report.result.passed; report=$reportPath; coldFailure=$report.result.coldFailure })
            $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding utf8
            if ($phaseExit -ne 0 -or -not $report.success) { throw "Policy phase failed: $reportPath" }
            Write-Output "PASS skip=$skip $phase ($($report.result.passed) assertions; process $($report.result.processId))"
        }
    }
} finally { $env:REHEARSAL_OUTPUT = $previousOutput }
Write-Output "Results: $runRoot"
