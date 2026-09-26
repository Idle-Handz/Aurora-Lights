# take-content-library.ps1
#
# Takes a newer Aurora.Content from the sibling AuroraTranslator checkout: vendors it,
# verifies it the way this repo's gates require, and leaves a commit on a branch for review.
# It does the sequence we otherwise do by hand, in the order that has caught real problems.
#
# Usage (from any directory):
#   .\tools\take-content-library.ps1                       # take it if there is a newer version
#   .\tools\take-content-library.ps1 -WhatIfVersion        # report versions and stop
#   .\tools\take-content-library.ps1 -Parity all           # force the full parity suite
#   .\tools\take-content-library.ps1 -Parity none          # skip parity (leaves the gate unmet)
#
# Nothing is pushed and no pull request is opened; the last step prints what to do next.
[CmdletBinding()]
param(
    [string]$TranslatorRepo,
    [ValidateSet('auto', 'db', 'all', 'none')][string]$Parity = 'auto',
    [string]$ParityBaseline,
    [switch]$AllowDirty,
    [switch]$NoCommit,
    [switch]$WhatIfVersion
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $TranslatorRepo) { $TranslatorRepo = Join-Path $PSScriptRoot '..\..\5eApiTranslator' }
$repo = Split-Path -Parent $PSScriptRoot
$translator = (Resolve-Path -LiteralPath $TranslatorRepo).Path
Set-Location -LiteralPath $repo

$steps = New-Object System.Collections.ArrayList
trap { Write-Host ""; $steps | Format-Table -AutoSize; break }
function Step {
    param([string]$Name, [scriptblock]$Body)
    $started = Get-Date
    Write-Host ""
    Write-Host "== $Name" -ForegroundColor Cyan
    $ok = $true
    try { & $Body } catch { $ok = $false; Write-Host $_.Exception.Message -ForegroundColor Red }
    $seconds = [int]((Get-Date) - $started).TotalSeconds
    [void]$steps.Add([pscustomobject]@{ Step = $Name; Result = $(if ($ok) { 'pass' } else { 'FAIL' }); Seconds = $seconds })
    if (-not $ok) { throw "$Name failed." }
}

function Invoke-Native {
    param([scriptblock]$Command)
    # Windows PowerShell wraps a native command's redirected stderr in an ErrorRecord, which
    # ErrorActionPreference=Stop then treats as fatal even when the command succeeded. Capture
    # the output without that, and judge the command by its exit code.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command 2>&1 } finally { $ErrorActionPreference = $previous }
}

function Get-RepoRelativePath {
    param([string]$Path)
    # Windows PowerShell has no [IO.Path]::GetRelativePath, and bash wants forward slashes.
    return ((Resolve-Path -LiteralPath $Path -Relative).TrimStart('.', '') -replace '\', '/')
}

function Get-DataVersion {
    param([string]$SqlitePath)
    if (-not (Test-Path -LiteralPath $SqlitePath)) { return $null }
    $script = "import sqlite3,sys;c=sqlite3.connect('file:'+sys.argv[1].replace(chr(92),'/')+'?mode=ro',uri=True);" +
              "print(c.execute('select data_version from database_metadata where singleton_id=1').fetchone()[0])"
    $value = & python -c $script $SqlitePath 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return [int]$value
}

# ── Is there anything to take? ───────────────────────────────────────────────
[xml]$props = Get-Content -LiteralPath (Join-Path $translator 'Aurora.Content.props') -Raw
$available = $props.Project.PropertyGroup.Version
$pinText = Get-Content -LiteralPath (Join-Path $repo 'AuroraContent.props') -Raw
$pinned = ([regex]::Match($pinText, '<AuroraContentVersion>([^<]*)</AuroraContentVersion>')).Groups[1].Value

Write-Host "pinned here : $pinned"
Write-Host "available   : $available  ($translator)"
if ($available -eq $pinned) {
    Write-Host "Already on the newest library; nothing to take." -ForegroundColor Green
    exit 0
}
if ([version]$available -lt [version]$pinned) {
    throw "The Translator offers $available, which is older than the pinned $pinned. Check out the right commit there first."
}
if ($WhatIfVersion) { exit 0 }

# ── Preconditions that otherwise fail late and confusingly ───────────────────
Step 'preconditions' {
    if (git -C $repo status --porcelain) { throw "This repo has uncommitted changes; commit or stash them first." }
    if ((git -C $translator status --porcelain) -and -not $AllowDirty) {
        throw "The Translator checkout is dirty. Commit there first, or pass -AllowDirty for a local experiment."
    }
    if (Get-Process -Name 'Aurora.Reflections' -ErrorAction SilentlyContinue) {
        throw "Aurora: Reflections is running and holds the build output. Close it and run this again."
    }
}

$branch = "content-library-$available"
Step "branch $branch" {
    Invoke-Native { git -C $repo checkout -b $branch } | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create branch $branch (does it already exist?)." }
}

Step "vendor $available" {
    $arguments = @('-TranslatorRepo', $translator)
    if ($AllowDirty) { $arguments += '-AllowDirty' }
    & (Join-Path $PSScriptRoot 'update-content-library.ps1') @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Vendoring failed.' }
}

# ── The gates, cheapest first ────────────────────────────────────────────────
Step 'build (Windows app)' {
    dotnet build (Join-Path $repo 'Aurora.App\Aurora.App.csproj') -f net10.0-windows10.0.19041.0 -m:1 -v:q --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The Windows app build failed.' }
}

Step 'tests' {
    dotnet test (Join-Path $repo 'Aurora.Tests\Aurora.Tests.csproj') -m:1 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Aurora.Tests failed.' }
}

Step 'legacy parity gates' {
    $gate = Join-Path $PSScriptRoot 'legacy-restoration\Compare-RestoredAssemblyApi.ps1'
    foreach ($assembly in 'Builder.Core', 'Builder.Data', 'Aurora.Documents') {
        & $gate -Configuration Release `
            -OracleAssembly (Join-Path $repo "tests\LegacyOracles\$assembly.dll") `
            -RestoredAssembly (Join-Path $repo "$assembly\bin\Release\net10.0\$assembly.dll")
        if ($LASTEXITCODE -ne 0) { throw "$assembly API gate failed." }
    }
    & (Join-Path $PSScriptRoot 'legacy-restoration\Compare-BuilderDataBehavior.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Builder.Data behaviour gate failed.' }
}

# ── Parity ───────────────────────────────────────────────────────────────────
if ($Parity -ne 'none') {
    if (-not $ParityBaseline) {
        $candidate = Get-ChildItem -Path (Join-Path $repo 'buildtmp') -Directory -Filter 'parity-rerun-*' -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.FullName 'results\characters') } |
            Sort-Object Name -Descending | Select-Object -First 1
        if (-not $candidate) { throw "No parity baseline found under buildtmp; pass -ParityBaseline." }
        $ParityBaseline = $candidate.FullName
    }
    Write-Host "parity baseline: $ParityBaseline"
    $baselineVersion = Get-DataVersion (Join-Path $ParityBaseline 'results\fresh-a.sqlite')

    $env:AURORA_CONTENT_SOURCE = 'pinned'
    $env:PARALLEL = '4'
    $label = "take-$available"
    $rerunRoot = $null

    Step 'parity (databases)' {
        $relative = Get-RepoRelativePath $ParityBaseline
        $output = Invoke-Native { bash 'tools/ContentDatabaseRehearsal/verify_content_library.sh' $relative $label 'db' }
        $output | ForEach-Object { Write-Host "   $_" }
        $line = $output | Where-Object { $_ -match '^rerun root: ' } | Select-Object -First 1
        if (-not $line) { throw 'The parity run did not report a rerun root.' }
        $script:rerunRoot = $line -replace '^rerun root: ', ''
    }

    $newVersion = Get-DataVersion (Join-Path $rerunRoot 'results\fresh-a.sqlite')
    Write-Host "data version: baseline $baselineVersion -> new $newVersion"
    $formatMoved = ($baselineVersion -ne $null -and $newVersion -ne $null -and $baselineVersion -ne $newVersion)
    $runCharacters = ($Parity -eq 'all') -or ($Parity -eq 'auto' -and $formatMoved)

    if ($runCharacters) {
        Step 'parity (characters)' {
            # The suite runs every character against the installed case, whose database was copied
            # from the baseline. Across a format change the new reader refuses it, and all of them
            # fail without comparing anything, so refresh that case first.
            if ($formatMoved) {
                $harness = Join-Path $repo "buildtmp\content-library-builds\$label\harness\ContentDatabaseRehearsal.exe"
                & $harness refresh (Join-Path $rerunRoot 'installed') | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Refreshing the installed case failed.' }
                & $harness dump (Join-Path $rerunRoot 'installed') | Out-Null
                Move-Item -LiteralPath (Join-Path $rerunRoot 'installed\projection-dump.json') `
                          -Destination (Join-Path $rerunRoot 'results\installed-projection.json') -Force
            }
            $env:HARNESS_EXE = "buildtmp/content-library-builds/$label/harness/ContentDatabaseRehearsal.exe"
            $relativeRerun = Get-RepoRelativePath $rerunRoot
            Invoke-Native { bash 'tools/ContentDatabaseRehearsal/run_parity_suite.sh' $relativeRerun 'characters' } |
                Select-Object -Last 3 | ForEach-Object { Write-Host "   $_" }

            # dataVersion differs by design across a format change, and Missing rides on every
            # load result; everything else must still match.
            $ignore = @()
            if ($formatMoved) { $ignore = @('--ignore-field', 'dataVersion', '--ignore-field', 'Missing') }
            $comparison = Invoke-Native { python 'tools/ContentDatabaseRehearsal/compare_to_baseline.py' @ignore $ParityBaseline $rerunRoot }
            $comparison | ForEach-Object { Write-Host "   $_" }
            if ($comparison -notmatch 'OVERALL: match') {
                throw 'Parity differs. Read the comparison above before taking this version.'
            }
        }
    }
}

# ── Leave it ready for review ────────────────────────────────────────────────
if (-not $NoCommit) {
    Step 'commit' {
        git -C $repo add AuroraContent.props vendor/nuget
        git -C $repo commit -m "Take Aurora.Content $available

Vendored from the AuroraTranslator checkout and verified here: the Windows
app builds, Aurora.Tests pass, all four legacy parity gates hold, and the
parity suite was run at the scope this script chose.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
        if ($LASTEXITCODE -ne 0) { throw 'Commit failed.' }
    }
}

Write-Host ""
$steps | Format-Table -AutoSize
Write-Host "Aurora.Content $available is vendored, verified and committed on $branch." -ForegroundColor Green
Write-Host "Nothing has been pushed. Review the commit, then merge or open a pull request yourself."
