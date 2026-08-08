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

$differences = @(Compare-Object -ReferenceObject $oracleApi -DifferenceObject $restoredApi)
if ($differences.Count -gt 0) {
    $differences | Format-Table -AutoSize
    throw "Assembly API comparison failed with $($differences.Count) differing line(s)."
}

Write-Output "API surface matches across $($oracleApi.Count) normalized line(s)."
