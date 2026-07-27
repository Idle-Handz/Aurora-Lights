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
$toolProject = Join-Path $repositoryRoot 'tools\WpfAssemblyApi\WpfAssemblyApi.csproj'

& dotnet build $toolProject --configuration $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "WpfAssemblyApi build failed with exit code $LASTEXITCODE."
}

$toolAssembly = Join-Path $repositoryRoot "tools\WpfAssemblyApi\bin\$Configuration\net10.0-windows\WpfAssemblyApi.dll"
$oraclePath = (Resolve-Path $OracleAssembly).Path
$restoredPath = (Resolve-Path $RestoredAssembly).Path

$oracleApi = @(& dotnet $toolAssembly $oraclePath)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect WPF oracle assembly '$oraclePath'."
}

$restoredApi = @(& dotnet $toolAssembly $restoredPath)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect restored WPF assembly '$restoredPath'."
}

$differences = @(Compare-Object -ReferenceObject $oracleApi -DifferenceObject $restoredApi)
if ($differences.Count -gt 0) {
    $differences | Format-Table -AutoSize
    throw "WPF assembly API comparison failed with $($differences.Count) differing line(s)."
}

Write-Output "WPF API surface matches across $($oracleApi.Count) normalized line(s)."
