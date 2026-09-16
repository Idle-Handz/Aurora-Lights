[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OracleAssembly,

    [Parameter(Mandatory = $true)]
    [string]$RestoredAssembly,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$toolProject = Join-Path $repositoryRoot 'tools\AssemblyApi\AssemblyApi.csproj'

& dotnet build $toolProject --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "AssemblyApi build failed with exit code $LASTEXITCODE."
}

$toolAssembly = Join-Path $repositoryRoot "tools\AssemblyApi\bin\$Configuration\net10.0\AssemblyApi.dll"
$dependencyDirectory = Join-Path $repositoryRoot 'lib'
$oraclePath = (Resolve-Path $OracleAssembly).Path
$restoredPath = (Resolve-Path $RestoredAssembly).Path

$oracleApi = @(& dotnet $toolAssembly $oraclePath $dependencyDirectory)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect oracle assembly '$oraclePath'."
}

$restoredApi = @(& dotnet $toolAssembly $restoredPath $dependencyDirectory)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect restored assembly '$restoredPath'."
}

. (Join-Path $PSScriptRoot 'ApiCompatibility.ps1')
$comparison = Compare-LegacyApi -OracleApi $oracleApi -RestoredApi $restoredApi
if ($comparison.Missing.Count -gt 0) {
    $comparison.Missing | ForEach-Object { Write-Output "Missing legacy API: $_" }
    throw "Assembly API compatibility failed: $($comparison.Missing.Count) required legacy signature(s) missing or changed."
}

Write-Output "Legacy API preserved: $($comparison.RequiredCount) required signatures; $($comparison.Added.Count) additions permitted."
