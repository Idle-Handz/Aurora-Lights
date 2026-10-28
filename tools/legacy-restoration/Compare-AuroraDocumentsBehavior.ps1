[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$TestFilter = 'FullyQualifiedName~AuroraDocumentsCompatibilityTests'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
# The two engines use different assembly identities. Compile identical behavior
# assertions separately against each engine instead of replacing one output DLL.
& dotnet test (Join-Path $repositoryRoot 'Aurora.Tests\Aurora.Tests.csproj') --configuration $Configuration --no-restore -m:1 --filter $TestFilter
if ($LASTEXITCODE -ne 0) { throw 'Source-built Aurora.Documents behavior tests failed.' }
& dotnet test (Join-Path $PSScriptRoot 'DocumentsOracleTests\DocumentsOracleTests.csproj') --configuration $Configuration -m:1
if ($LASTEXITCODE -ne 0) { throw 'Legacy Aurora.Documents oracle behavior tests failed.' }
Write-Output 'Identical Aurora.Documents assertions passed against the restored source and the immutable legacy oracle.'
