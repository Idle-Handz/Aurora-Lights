[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$TestFilter = 'FullyQualifiedName~AuroraPresentationCompatibilityTests'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repositoryRoot 'Aurora.Presentation.Tests\Aurora.Presentation.Tests.csproj'
$testOutput = Join-Path $repositoryRoot "Aurora.Presentation.Tests\bin\$Configuration\net10.0-windows"
$restoredAssembly = Join-Path $repositoryRoot "Aurora.Presentation\bin\$Configuration\net10.0-windows\Aurora.Presentation.dll"
$oracleAssembly = Join-Path $repositoryRoot 'tests\LegacyOracles\Aurora.Presentation.dll'

& dotnet test $testProject `
    --configuration $Configuration `
    --no-restore `
    --filter $TestFilter
if ($LASTEXITCODE -ne 0) {
    throw "Restored Aurora.Presentation behavior tests failed with exit code $LASTEXITCODE."
}

$testAssembly = Join-Path $testOutput 'Aurora.Presentation.Tests.dll'
$sourceOutputAssembly = Join-Path $testOutput 'Aurora.Presentation.dll'
foreach ($requiredPath in @($testAssembly, $sourceOutputAssembly, $restoredAssembly, $oracleAssembly)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required comparison artifact not found: $requiredPath"
    }
}

$sourceOutputHash = (Get-FileHash -LiteralPath $sourceOutputAssembly -Algorithm SHA256).Hash
$restoredHash = (Get-FileHash -LiteralPath $restoredAssembly -Algorithm SHA256).Hash
if ($sourceOutputHash -ne $restoredHash) {
    throw 'Aurora.Presentation.Tests output does not contain the source-built Aurora.Presentation assembly.'
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$oracleRun = Join-Path $tempRoot ('aurora-presentation-oracle-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $oracleRun | Out-Null

try {
    Copy-Item -Path (Join-Path $testOutput '*') -Destination $oracleRun -Recurse
    Copy-Item -LiteralPath $oracleAssembly -Destination (Join-Path $oracleRun 'Aurora.Presentation.dll') -Force

    & dotnet vstest (Join-Path $oracleRun 'Aurora.Presentation.Tests.dll') `
        "--TestCaseFilter:$TestFilter"
    if ($LASTEXITCODE -ne 0) {
        throw "Production Aurora.Presentation behavior tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    $resolvedOracleRun = [IO.Path]::GetFullPath($oracleRun)
    if (
        $resolvedOracleRun.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedOracleRun).StartsWith(
            'aurora-presentation-oracle-tests-',
            [StringComparison]::Ordinal)
    ) {
        Remove-Item -LiteralPath $resolvedOracleRun -Recurse -Force
    }
}

Write-Output 'Aurora.Presentation compatibility tests pass against both restored source and the production oracle.'
