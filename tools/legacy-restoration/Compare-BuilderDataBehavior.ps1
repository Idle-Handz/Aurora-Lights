[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$TestFilter = 'FullyQualifiedName~BuilderDataCompatibilityTests'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testProject = Join-Path $repositoryRoot 'Aurora.Tests\Aurora.Tests.csproj'
$testOutput = Join-Path $repositoryRoot "Aurora.Tests\bin\$Configuration\net10.0"
$restoredAssembly = Join-Path $repositoryRoot "Builder.Data\bin\$Configuration\net10.0\Builder.Data.dll"
$oracleAssembly = Join-Path $repositoryRoot 'lib\Builder.Data.dll'

& dotnet test $testProject `
    --configuration $Configuration `
    --no-restore `
    --filter $TestFilter
if ($LASTEXITCODE -ne 0) {
    throw "Restored Builder.Data behavior tests failed with exit code $LASTEXITCODE."
}

$testAssembly = Join-Path $testOutput 'Aurora.Tests.dll'
$sourceOutputAssembly = Join-Path $testOutput 'Builder.Data.dll'
foreach ($requiredPath in @($testAssembly, $sourceOutputAssembly, $restoredAssembly, $oracleAssembly)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required comparison artifact not found: $requiredPath"
    }
}

$sourceOutputHash = (Get-FileHash -LiteralPath $sourceOutputAssembly -Algorithm SHA256).Hash
$restoredHash = (Get-FileHash -LiteralPath $restoredAssembly -Algorithm SHA256).Hash
if ($sourceOutputHash -ne $restoredHash) {
    throw 'Aurora.Tests output does not contain the source-built Builder.Data assembly.'
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$oracleRun = Join-Path $tempRoot ('aurora-builder-data-oracle-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $oracleRun | Out-Null

try {
    Copy-Item -Path (Join-Path $testOutput '*') -Destination $oracleRun -Recurse
    Copy-Item -LiteralPath $oracleAssembly -Destination (Join-Path $oracleRun 'Builder.Data.dll') -Force

    & dotnet vstest (Join-Path $oracleRun 'Aurora.Tests.dll') `
        "--TestCaseFilter:$TestFilter"
    if ($LASTEXITCODE -ne 0) {
        throw "Production Builder.Data behavior tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    $resolvedOracleRun = [IO.Path]::GetFullPath($oracleRun)
    if (
        $resolvedOracleRun.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedOracleRun).StartsWith(
            'aurora-builder-data-oracle-tests-',
            [StringComparison]::Ordinal)
    ) {
        Remove-Item -LiteralPath $resolvedOracleRun -Recurse -Force
    }
}

Write-Output 'Builder.Data compatibility tests pass against both restored source and the production oracle.'
